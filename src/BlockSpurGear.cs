using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.GameContent;

namespace SpurGears
{
    /// <summary>
    /// Variants: teeth (12/24/36) x rotation (ns/we/ud) x ends (both/a/b).
    /// "ends" = on which side of the gear the shaft stub (and the power connector) exists.
    /// Wrench: cycles both -> a -> b (left click = reverse direction).
    /// </summary>
    public class BlockSpurGear : BlockMPBase, IWrenchOrientable
    {
        public string Rotation => Variant["rotation"];          // ns | we | ud
        public string Ends => Variant["ends"];                  // both | a | b
        public int Teeth => int.Parse(Variant["teeth"]);

        /// <summary>
        /// Facing of the shaft-local "z = 0" end of the shape (end "a"). With the shape rotations in the block json:
        /// ns: rotateY 0 -> NORTH, we: rotateY 90 -> WEST, ud: rotateX 90 -> UP.
        /// If on vertical gears stub "a" shows up on the wrong end, swap UP/DOWN here.
        /// </summary>
        public BlockFacing EndAFacing => Rotation switch
        {
            "ns" => BlockFacing.NORTH,
            "we" => BlockFacing.WEST,
            _ => BlockFacing.UP,
        };

        public bool IsAxisFace(BlockFacing face) => face == EndAFacing || face == EndAFacing.Opposite;

        /// <summary>true if this axis face has a shaft stub / connector</summary>
        public bool HasStub(BlockFacing face)
        {
            switch (Ends)
            {
                case "a": return face == EndAFacing;
                case "b": return face == EndAFacing.Opposite;
                default: return IsAxisFace(face);
            }
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (IsAxisFace(face)) return HasStub(face);

            // side faces: only mesh with a gear on the same axis
            var nb = world.BlockAccessor.GetBlock(pos.AddCopy(face)) as BlockSpurGear;
            return nb != null && nb.Rotation == Rotation;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        void ConnectAll(IWorldAccessor world, BlockPos pos)
        {
            var self = world.BlockAccessor.GetBlock(pos) as BlockSpurGear;
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockPos npos = pos.AddCopy(face);
                var nblock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                if (nblock == null) continue;
                if (!self.HasMechPowerConnectorAt(world, pos, face)) continue;
                if (!nblock.HasMechPowerConnectorAt(world, npos, face.Opposite)) continue;

                nblock.DidConnectAt(world, npos, face.Opposite);
                WasPlaced(world, pos, face);
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            // axis: along the clicked face normal; clicking the side of another spur gear copies its axis so they can mesh
            string rot = blockSel.Face.Axis switch
            {
                EnumAxis.X => "we",
                EnumAxis.Y => "ud",
                _ => "ns"
            };
            var clicked = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face.Opposite)) as BlockSpurGear;
            if (clicked != null) rot = clicked.Rotation;

            Block toPlace = world.GetBlock(CodeWithVariant("rotation", rot));
            if (toPlace == null || !toPlace.DoPlaceBlock(world, byPlayer, blockSel, itemstack)) return false;

            ConnectAll(world, blockSel.Position);
            return true;
        }

        // ---- wrench ----------------------------------------------------------------------

        static readonly string[] EndsOrder = { "both", "a", "b" };

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            IWorldAccessor world = byEntity.World;
            if (world.Side != EnumAppSide.Server) return;   // the server does the work, the client gets the block update

            int i = System.Array.IndexOf(EndsOrder, Ends);
            i = (i + (dir >= 0 ? 1 : EndsOrder.Length - 1)) % EndsOrder.Length;

            Block next = world.GetBlock(CodeWithVariant("ends", EndsOrder[i]));
            if (next == null) return;

            BlockPos pos = blockSel.Position;
            var bh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();

            world.BlockAccessor.ExchangeBlock(next.Id, pos);   // keeps the block entity
            bh?.ReattachAfterVariantChange();                  // drop the old connections, rebuild networks around us
            ConnectAll(world, pos);                            // and connect through whatever stubs we have now
        }
    }
}
