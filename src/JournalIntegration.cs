using System;
using System.Reflection;

namespace ErenshorContracts
{
    internal static class JournalIntegration
    {
        private static MethodInfo _addChronicle;
        private static bool _resolved;

        internal static bool TryAppend(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            Resolve();
            if (_addChronicle == null) return false;

            try
            {
                object result = _addChronicle.Invoke(null, new object[] { "Erenshor Contracts", "Contract", text });
                return result is bool && (bool)result;
            }
            catch
            {
                _resolved = false;
                _addChronicle = null;
                return false;
            }
        }

        internal static bool IsAvailable
        {
            get
            {
                Resolve();
                return _addChronicle != null;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                Type api = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length && api == null; i++)
                    api = assemblies[i].GetType("ErenshorJournal.JournalApi", false);
                if (api == null) return;

                _addChronicle = api.GetMethod(
                    "AddChronicleEntry",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(string), typeof(string) },
                    null);
            }
            catch
            {
                _addChronicle = null;
            }
        }
    }
}
