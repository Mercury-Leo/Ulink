using UnityEngine;
using UnityEngine.Localization;

namespace Ulink.Runtime.Localization
{
    internal static class UlinkLocalizationRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            UlinkValueConverter.RegisterConverter(typeof(LocalizedString), raw =>
            {
                if (string.IsNullOrEmpty(raw)) return null;
                int sep = raw.IndexOf(UlinkSeparators.FieldValueSeparator);
                return sep < 0 ? null : new LocalizedString(raw[..sep], raw[(sep + 1)..]);
            });
        }
    }
}
