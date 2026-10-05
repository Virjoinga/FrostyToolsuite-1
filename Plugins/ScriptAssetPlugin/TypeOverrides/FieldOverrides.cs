using FrostySdk.Attributes;
using FrostySdk;

namespace ScriptAssetPlugin.TypeOverrides
{
    public class SchedulableJsonAssetOverride : BaseTypeOverride
    {
        [IsHidden]
        public BaseFieldOverride DefaultJson { get; set; }
    }
    public class UIFontEffectAssetOverride : BaseTypeOverride
    {
        [IsHidden]
        public BaseFieldOverride EffectScript { get; set; }
    }
}
