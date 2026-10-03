using Game.Core;
using Nixin.Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Unity
{
    public sealed class HuntHomeScreen : MonoBehaviour
    {
        const string HomePanelName = "HomePanel";

        [SerializeField] HuntCatalogAsset catalogAsset;

        HuntLevelSelectScreen _levelSelect;
        HuntCatalog _catalog;

        void Start()
        {
            HuntUiInput.EnsureEventSystem();
            HuntMenuUi.EnsureCanvas(gameObject);
            EnsureUi();
            WireHomeButtons();
            EnsureDebugCrashButton();
            GameServices.Analytics.TrackScreenView("HuntHomeScreen");
        }

        void EnsureUi()
        {
            if (transform.Find(HomePanelName) != null)
            {
                _catalog = ResolveCatalog();
                _levelSelect = GetComponent<HuntLevelSelectScreen>();
                if (_levelSelect == null)
                    _levelSelect = gameObject.AddComponent<HuntLevelSelectScreen>();
                _levelSelect.BindCatalog(_catalog);
                return;
            }

            PurgeMenuChildren();
            BuildUi();
        }

        void PurgeMenuChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        public void Build()
        {
            HuntMenuUi.EnsureCanvas(gameObject);
            PurgeMenuChildren();
            BuildUi();
            WireHomeButtons();
        }

        void BuildUi()
        {
            var panel = HuntMenuUi.MakePanel(HomePanelName, transform, new Color(0.12f, 0.14f, 0.2f, 1f));
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
                panelImage.raycastTarget = false;

            var title = HuntMenuUi.MakeText("Title", panel.transform, 56, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.text = "Hidden Mosaic";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.1f, 0.72f);
            titleRect.anchorMax = new Vector2(0.9f, 0.88f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var play = HuntMenuUi.MakeButton(panel.transform, "Play", new Vector2(420f, 72f), new Color(0.18f, 0.42f, 0.28f, 0.95f));
            var playRect = play.GetComponent<RectTransform>();
            playRect.anchorMin = new Vector2(0.5f, 0.48f);
            playRect.anchorMax = new Vector2(0.5f, 0.48f);
            playRect.anchoredPosition = Vector2.zero;
            play.onClick.AddListener(OnPlay);

            var levels = HuntMenuUi.MakeButton(panel.transform, "Select Levels", new Vector2(420f, 72f), new Color(0.22f, 0.32f, 0.48f, 0.95f));
            var levelsRect = levels.GetComponent<RectTransform>();
            levelsRect.anchorMin = new Vector2(0.5f, 0.36f);
            levelsRect.anchorMax = new Vector2(0.5f, 0.36f);
            levelsRect.anchoredPosition = Vector2.zero;
            levels.onClick.AddListener(OnSelectLevels);

            _catalog = ResolveCatalog();
            _levelSelect = GetComponent<HuntLevelSelectScreen>();
            if (_levelSelect == null)
                _levelSelect = gameObject.AddComponent<HuntLevelSelectScreen>();
            _levelSelect.Build(transform, _catalog);
        }

        HuntCatalog ResolveCatalog()
        {
            if (catalogAsset != null && catalogAsset.Count > 0)
                return catalogAsset.ToCatalog();
            return HuntCatalogLoader.LoadJsonFromResources();
        }

        void WireHomeButtons()
        {
            var panel = transform.Find(HomePanelName);
            if (panel == null)
            {
                var legacy = transform.Find("Bg");
                if (legacy == null)
                    return;
                panel = legacy;
            }

            HuntMenuUi.WireClick(panel, "PlayButton", OnPlay);
            HuntMenuUi.WireClick(panel, "Select LevelsButton", OnSelectLevels);
        }

        void OnPlay()
        {
            GameServices.Analytics.TrackButtonClick("play", "HuntHomeScreen");
            HuntLaunchRequest.SetCampaign(0);
            SceneManager.LoadScene(HuntScenes.Play);
        }

        void OnSelectLevels()
        {
            GameServices.Analytics.TrackButtonClick("select_levels", "HuntHomeScreen");
            if (_levelSelect != null)
                _levelSelect.Show();
        }

        // Crashlytics test button. Only exists in Editor / Development builds.
        void EnsureDebugCrashButton()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var panel = transform.Find(HomePanelName);
            if (panel == null || panel.Find("Test CrashButton") != null)
                return;
            var btn = HuntMenuUi.MakeButton(panel, "Test Crash", new Vector2(420f, 72f), new Color(0.6f, 0.15f, 0.15f, 0.95f));
            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.24f);
            rect.anchorMax = new Vector2(0.5f, 0.24f);
            rect.anchoredPosition = Vector2.zero;
            btn.onClick.AddListener(OnTestCrash);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void OnTestCrash()
        {
            GameServices.CrashReporter.Log("Test breadcrumb before crash");
            throw new System.Exception("Test crash from HOGGame");
        }
#endif
    }
}
