using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;

[assembly: MelonInfo(typeof(AutoDispatcher.AutoDispatcherMod), "AutoDispatcher", "1.1.0", "V1ndicate1")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace AutoDispatcher
{
    public class AutoDispatcherMod : MelonMod
    {
        public static AutoDispatcherMod Instance { get; private set; }

        private static MelonPreferences_Category _prefCat;
        private static MelonPreferences_Entry<bool> _prefEnabled;
        private static MelonPreferences_Entry<bool> _prefEolEnabled;
        private static MelonPreferences_Entry<bool> _prefWarnEnabled;

        public static bool IsEnabled     => _prefEnabled?.Value     ?? false;
        public static bool IsEolEnabled  => _prefEolEnabled?.Value  ?? false;
        public static bool IsWarnEnabled => _prefWarnEnabled?.Value ?? false;

        public static void SetEnabled(bool value)     { _prefEnabled.Value     = value; MelonPreferences.Save(); }
        public static void SetEolEnabled(bool value)  { _prefEolEnabled.Value  = value; MelonPreferences.Save(); }
        public static void SetWarnEnabled(bool value) { _prefWarnEnabled.Value = value; MelonPreferences.Save(); }

        private float _pollTimer = 0f;
        private const float POLL_INTERVAL = 2f;

        public override void OnInitializeMelon()
        {
            Instance = this;

            _prefCat         = MelonPreferences.CreateCategory("AutoDispatcher", "Auto Dispatcher");
            _prefEnabled     = _prefCat.CreateEntry<bool>("Enabled",     false, "Auto-Dispatch Enabled");
            _prefEolEnabled  = _prefCat.CreateEntry<bool>("EolEnabled",  false, "EOL Dispatch Enabled");
            _prefWarnEnabled = _prefCat.CreateEntry<bool>("WarnEnabled", false, "Suppress Device Warnings");

            SaveSystem.onLoadingDataLater += (System.Action)DispatchController.RebuildFromGameState;

            MelonLogger.Msg($"[AutoDispatcher] v1.1.0 loaded. Enabled={IsEnabled} EolEnabled={IsEolEnabled} WarnEnabled={IsWarnEnabled}");
        }

        public override void OnUpdate()
        {
            _pollTimer += Time.deltaTime;
            if (_pollTimer >= POLL_INTERVAL)
            {
                _pollTimer = 0f;
                DispatchController.Poll();
            }
        }

        public override void OnLateUpdate()
        {
            // Kill Unity's selection highlight every frame — prevents blue box artifact
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
