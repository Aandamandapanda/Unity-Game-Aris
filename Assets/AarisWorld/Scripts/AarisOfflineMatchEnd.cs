using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Aaris.MapReview
{
    [DisallowMultipleComponent, RequireComponent(typeof(AarisOfflineHealth))]
    public sealed class AarisOfflineMatchEnd : MonoBehaviour
    {
        private AarisOfflineHealth health;
        private Button restartButton;
        private Text restartLabel;
        private float previousTimeScale;
        private bool previousAudioPause, pausedForDefeat;

        private void Awake()
        {
            health = GetComponent<AarisOfflineHealth>();
            health.Died += EndMatch;
            if (health.IsDead) EndMatch(health);
        }

        private void EndMatch(AarisOfflineHealth defeated)
        {
            if (pausedForDefeat) return;
            CreateDefeatScreen();
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            pausedForDefeat = true;

            // Update still runs at timeScale 0. Stop systems that can
            // accept input or stream loot using unscaled time.
            Disable(GetComponent<AarisThirdPersonPlayer>());
            Disable(GetComponent<AarisBattleActions>());
            Disable(GetComponent<AarisWeaponInventory>());
            Disable(GetComponent<AarisOfflineGlooWall>());
            Disable(GetComponent<AarisOfflineTacticalHUD>());
            Disable(GetComponent<AarisVehicleSession>());
            Disable(FindFirstObjectByType<AarisWeaponLootWorld>());
            var hud = FindFirstObjectByType<AarisBattleHUD>();
            if (hud != null) hud.CancelVehicleInputs();
        }

        private static void Disable(Behaviour behaviour)
        {
            if (behaviour != null) behaviour.enabled = false;
        }

        private void CreateDefeatScreen()
        {
            var overlay = new GameObject("Defeat Screen", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = .5f;

            // The full-screen graphic prevents touches reaching a lower HUD.
            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(overlay.transform, false);
            Fill(shade.GetComponent<RectTransform>());
            shade.GetComponent<Image>().color = new Color(.04f, .06f, .08f, .92f);

            var title = MakeText(shade.transform, "Defeated", 42);
            title.rectTransform.sizeDelta = new Vector2(400f, 70f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 36f);

            var action = new GameObject("Play Again", typeof(RectTransform), typeof(Image), typeof(Button));
            action.transform.SetParent(shade.transform, false);
            var rect = action.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 60f);
            rect.anchoredPosition = new Vector2(0f, -43f);
            var image = action.GetComponent<Image>();
            image.color = new Color(.23f, .36f, .42f);
            restartButton = action.GetComponent<Button>();
            restartButton.targetGraphic = image;
            restartButton.onClick.AddListener(Restart);
            restartLabel = MakeText(action.transform, "PLAY AGAIN", 22);
            Fill(restartLabel.rectTransform);
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text MakeText(Transform parent, string message, int size)
        {
            var obj = new GameObject(message, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = message;
            return label;
        }

        private void Restart()
        {
            if (!restartButton.interactable) return;
            restartButton.interactable = false;
            restartLabel.text = "LOADING";
            try
            {
                var current = gameObject.scene;
                if (current.buildIndex >= 0 &&
                    SceneManager.LoadSceneAsync(current.buildIndex, LoadSceneMode.Single) != null)
                    return;
            }
            catch (ArgumentException error)
            {
                Debug.LogWarning("Aaris match restart failed: " + error.GetType().Name);
            }
            restartLabel.text = "RETRY";
            restartButton.interactable = true;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= EndMatch;
            if (!pausedForDefeat) return;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }
    }
}
