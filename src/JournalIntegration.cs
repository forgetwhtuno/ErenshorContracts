using System;
using System.Reflection;

namespace ErenshorContracts
{
    internal static class JournalIntegration
    {
        private const int SupportedContractVersion = 1;

        private static int _resolvedAssemblyCount = -1;
        private static PropertyInfo _isAvailable;
        private static MethodInfo _addChronicle;

        internal static bool TryAppend(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !ProviderAvailable()) return false;
            try
            {
                object result = _addChronicle.Invoke(null, new object[] { "Erenshor Contracts", "Contract", text });
                return result is bool && (bool)result;
            }
            catch
            {
                Invalidate();
                return false;
            }
        }

        internal static bool IsAvailable { get { return ProviderAvailable(); } }

        private static bool ProviderAvailable()
        {
            Resolve();
            if (_isAvailable == null || _addChronicle == null) return false;
            try { return (bool)_isAvailable.GetValue(null, null); }
            catch
            {
                Invalidate();
                return false;
            }
        }

        private static void Resolve()
        {
            Assembly[] assemblies;
            try { assemblies = AppDomain.CurrentDomain.GetAssemblies(); }
            catch { return; }

            if (_resolvedAssemblyCount == assemblies.Length && _isAvailable != null && _addChronicle != null)
                return;

            _resolvedAssemblyCount = assemblies.Length;
            _isAvailable = null;
            _addChronicle = null;

            PropertyInfo inactiveAvailable = null;
            MethodInfo inactiveAdd = null;

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type api;
                try { api = assemblies[i].GetType("ErenshorJournal.JournalApi", false); }
                catch { continue; }
                if (api == null) continue;

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
                FieldInfo version = api.GetField("ContractVersion", flags);
                PropertyInfo available = api.GetProperty("IsAvailable", flags);
                MethodInfo add = api.GetMethod("AddChronicleEntry", flags, null,
                    new Type[] { typeof(string), typeof(string), typeof(string) }, null);

                if (version == null || version.FieldType != typeof(int) ||
                    available == null || available.PropertyType != typeof(bool) ||
                    add == null || add.ReturnType != typeof(bool))
                    continue;

                int contractVersion;
                try { contractVersion = (int)version.GetValue(null); }
                catch { continue; }
                if (contractVersion != SupportedContractVersion) continue;

                bool live = false;
                try { live = (bool)available.GetValue(null, null); } catch { }
                if (live)
                {
                    _isAvailable = available;
                    _addChronicle = add;
                    return;
                }

                if (inactiveAvailable == null)
                {
                    inactiveAvailable = available;
                    inactiveAdd = add;
                }
            }

            _isAvailable = inactiveAvailable;
            _addChronicle = inactiveAdd;
        }

        private static void Invalidate()
        {
            _resolvedAssemblyCount = -1;
            _isAvailable = null;
            _addChronicle = null;
        }
    }
}
