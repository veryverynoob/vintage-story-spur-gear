using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;

namespace SpurGears
{
    public class SpurGearsMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockSpurGear", typeof(BlockSpurGear));
            api.RegisterBlockEntityBehaviorClass("MPSpurGear", typeof(BEBehaviorMPSpurGear));
        }
    }
}
