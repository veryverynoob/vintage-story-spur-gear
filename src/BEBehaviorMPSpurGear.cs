using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace SpurGears
{
    /// <summary>
    /// A spur gear on an axle line (ns/we/ud). Along its axis it behaves like an axle (only through the ends that have a stub).
    /// With a neighbouring spur gear on the same axis it meshes: speed ratio = teeth_this / teeth_neighbour,
    /// and the neighbour turns the opposite way.
    ///
    /// Meshing is encoded in the MechPowerPath like the vanilla LargeGear3m does: gearingRatio is the ratio of the
    /// RECEIVING block; for a side entry the direction is passed via 'invert' (SideSign / WantedNeighbourDir) and the
    /// receiver converts it into its own axis direction.
    /// </summary>
    public class BEBehaviorMPSpurGear : BEBehaviorMPBase
    {
        // always read the block from the world: after a wrench ExchangeBlock the cached Blockentity.Block may be stale
        BlockSpurGear Gear => (Api?.World.BlockAccessor.GetBlock(Position) ?? Blockentity.Block) as BlockSpurGear;

        int Teeth => Gear.Teeth;
        string rotation => Gear.Rotation;
        BlockFacing posFacing => rotation == "ns" ? BlockFacing.SOUTH : rotation == "we" ? BlockFacing.EAST : BlockFacing.UP;
        BlockFacing negFacing => posFacing.Opposite;

        public BEBehaviorMPSpurGear(BlockEntity be) : base(be) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);   // base resets AxisSign to {0,0,1}
            SetOrientations();
            if (!OnAxis(propagationDir)) propagationDir = negFacing;
        }

        public override void SetOrientations()
        {
            var g = Gear;
            if (g == null) return;   // called from FromTreeAttributes before Api is set
            switch (g.Rotation)
            {
                case "ns": AxisSign = new[] { 0, 0, -1 }; break;   // same signs as the vanilla axle
                case "we": AxisSign = new[] { -1, 0, 0 }; break;
                default:   AxisSign = new[] { 0, 1, 0 }; break;
            }
        }

        protected override CompositeShape GetShape() => Gear?.Shape ?? base.GetShape();

        protected override void updateShape(IWorldAccessor worldForResolve)
        {
            Shape = worldForResolve.BlockAccessor.GetBlock(Position)?.Shape ?? Block?.Shape;
        }

        public override float GetResistance() => 0.0005f + Teeth * 0.00002f;

        bool OnAxis(BlockFacing f) => f == posFacing || f == negFacing;
        static int SideSign(BlockFacing f) => (f == BlockFacing.EAST || f == BlockFacing.SOUTH || f == BlockFacing.UP) ? 1 : -1;
        BlockFacing AxisDirFromSign(int s) => s > 0 ? posFacing : negFacing;

        /// <summary>the axis direction the meshing neighbour must take (opposite of ours)</summary>
        BlockFacing WantedNeighbourDir() => propagationDir.Opposite;

        /// <summary>teeth of a same-axis spur gear on the given side, or 0</summary>
        int NeighbourTeeth(BlockFacing side)
        {
            var nb = Api.World.BlockAccessor.GetBlock(Position.AddCopy(side)) as BlockSpurGear;
            return (nb != null && nb.Rotation == rotation) ? nb.Teeth : 0;
        }

        // ---- wrench: called by the block after ExchangeBlock to another "ends" variant --------

        public void ReattachAfterVariantChange()
        {
            OnBlockRemoved();            // leaves the network; networks that ran through us are rebuilt with the new connectors
            Shape = null;                // OnBlockRemoved already took us out of the renderer
            Shape = Gear.Shape;
            manager.AddDeviceForRender(this);
            SetOrientations();
            Blockentity.MarkDirty();
        }

        // ---- direction / ratio bookkeeping -----------------------------------------------

        public override void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            if (!OnAxis(turnDir)) turnDir = AxisDirFromSign(SideSign(turnDir));   // entry from a meshing neighbour

            if (propagationDir == turnDir.Opposite && network != null)
            {
                if (!network.DirectionHasReversed)
                    network.TurnDir = network.TurnDir == EnumRotDirection.Clockwise ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
                network.DirectionHasReversed = true;
            }
            propagationDir = turnDir;
            GearedRatio = path.gearingRatio;
        }

        public override bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            if (OnAxis(test)) return propagationDir == test;
            // side face: answer so that tryConnect() builds a path the neighbour decodes as "opposite of us"
            return SideSign(test) == (WantedNeighbourDir() == posFacing ? 1 : -1);
        }

        public override float GetGearedRatio(BlockFacing face)
        {
            if (OnAxis(face)) return GearedRatio;
            // called by a freshly placed neighbour located at Position + face.Opposite
            int nt = NeighbourTeeth(face.Opposite);
            return nt == 0 ? GearedRatio : GearedRatio * Teeth / nt;
        }

        protected override MechPowerPath[] GetMechPowerExits(MechPowerPath entry)
        {
            float r = GearedRatio;   // our own ratio, already set from the entry path
            var g = Gear;
            var list = new List<MechPowerPath>(6);

            // along the axle line, but only through ends that actually have a stub
            if (g.HasStub(propagationDir)) list.Add(new MechPowerPath(propagationDir, r, Position, false));
            if (g.HasStub(propagationDir.Opposite)) list.Add(new MechPowerPath(propagationDir.Opposite, r, Position, true));

            int want = WantedNeighbourDir() == posFacing ? 1 : -1;
            foreach (BlockFacing side in BlockFacing.ALLFACES)
            {
                if (OnAxis(side)) continue;
                int nt = NeighbourTeeth(side);
                if (nt == 0) continue;
                bool invert = SideSign(side) != want;
                list.Add(new MechPowerPath(side, r * Teeth / nt, Position, invert));
            }
            return list.ToArray();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            sb.AppendLine($"Teeth: {Teeth}");
        }
    }
}
