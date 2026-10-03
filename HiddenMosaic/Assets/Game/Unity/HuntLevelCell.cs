using System;
using System.IO;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    public sealed class HuntLevelCell : MonoBehaviour
    {
        Image _preview;
        Text _label;
        Button _button;
        int _index;

        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);
            var rect = transform as RectTransform;
            if (rect == null)
                rect = gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 380f);

            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.14f, 0.15f, 0.18f, 0.95f);

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            previewGo.transform.SetParent(transform, false);
            var previewRect = previewGo.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.1f, 0.28f);
            previewRect.anchorMax = new Vector2(0.9f, 0.92f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            _preview = previewGo.GetComponent<Image>();
            _preview.preserveAspect = true;
            _preview.color = Color.white;

            _label = HuntMenuUi.MakeText("Label", transform, 26, FontStyle.Normal, TextAnchor.UpperCenter);
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0.05f, 0.02f);
            labelRect.anchorMax = new Vector2(0.95f, 0.26f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = bg.GetComponent<Image>();
            _button.onClick.AddListener(OnClick);
        }

        public void Bind(int index, HuntLevelDef def, Action<int> onPick)
        {
            _index = index;
            _onPick = onPick;
            var stars = new string('★', def.Toughness) + new string('☆', 5 - def.Toughness);
            _label.text = (index + 1) + ". " + def.Title + "\n" + def.TargetCount + " icons  " + stars;

            var stem = Path.GetFileNameWithoutExtension(def.FileName);
            var key = HuntContentResources.TextKey(HuntCatalogLoader.SilhouetteFolder, stem + ".png");
            var tex = Resources.Load<Texture2D>(key);
            if (tex != null)
            {
                _preview.sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
            }
            else
                _preview.sprite = null;
        }

        Action<int> _onPick;

        void OnClick()
        {
            _onPick?.Invoke(_index);
        }
    }
}
