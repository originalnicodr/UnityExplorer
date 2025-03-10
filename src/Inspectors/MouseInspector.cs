﻿using UnityExplorer.Config;
using UnityExplorer.Inspectors.MouseInspectors;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;
using UniverseLib.Input;
using UniverseLib.UI;
using UniverseLib.UI.Panels;

namespace UnityExplorer.Inspectors
{
    public enum MouseInspectMode
    {
        World,
        UI
    }

    public class MouseInspector : PanelBase
    {
        public static MouseInspector Instance { get; private set; }

        private readonly WorldInspector worldInspector;
        private readonly UiInspector uiInspector;

        public static bool Inspecting { get; set; }
        public static MouseInspectMode Mode { get; set; }

        public MouseInspectorBase CurrentInspector => Mode switch
        {
            MouseInspectMode.UI => uiInspector,
            MouseInspectMode.World => worldInspector,
            _ => null,
        };

        private static Vector3 lastMousePos;

        // UIPanel
        internal static readonly string UIBaseGUID = $"{ExplorerCore.GUID}.MouseInspector";
        internal static UIBase inspectorUIBase;

        public override string Name => "Inspect Under Mouse";
        public override int MinWidth => -1;
        public override int MinHeight => -1;
        public override Vector2 DefaultAnchorMin => Vector2.zero;
        public override Vector2 DefaultAnchorMax => Vector2.zero;

        public override bool CanDragAndResize => false;
        private Action<GameObject> inspectorAction = null;

        private Text inspectorLabelTitle;
        private Text objNameLabel;
        private Text objPathLabel;
        private Text mousePosLabel;

        public MouseInspector(UIBase owner) : base(owner)
        {
            Instance = this;
            worldInspector = new WorldInspector();
            uiInspector = new UiInspector();
        }

        public static void OnDropdownSelect(int index)
        {
            switch (index)
            {
                case 0: return;
                case 1: Instance.StartInspect(MouseInspectMode.World, (obj) => InspectorManager.Inspect(obj, null)); break;
                case 2: Instance.StartInspect(MouseInspectMode.UI, (obj) => InspectorManager.Inspect(obj, null)); break;
            }
            InspectorPanel.Instance.MouseInspectDropdown.value = 0;
        }

        public void StartInspect(MouseInspectMode mode, Action<GameObject> newInspectorAction)
        {
            Mode = mode;
            Inspecting = true;

            CurrentInspector.OnBeginMouseInspect();

            PanelManager.ForceEndResize();
            UIManager.NavBarRect.gameObject.SetActive(false);
            UIManager.UiBase.Panels.PanelHolder.SetActive(false);
            UIManager.UiBase.SetOnTop();

            SetActive(true);

            inspectorAction = newInspectorAction;
        }

        internal void ClearHitData()
        {
            CurrentInspector.ClearHitData();

            objNameLabel.text = "No hits...";
            objPathLabel.text = "";
        }

        public void StopInspect()
        {
            CurrentInspector.OnEndInspect();
            ClearHitData();
            Inspecting = false;

            UIManager.NavBarRect.gameObject.SetActive(true);
            UIManager.UiBase.Panels.PanelHolder.SetActive(true);

            Dropdown drop = InspectorPanel.Instance.MouseInspectDropdown;
            if (drop.transform.Find("Dropdown List") is Transform list)
                drop.DestroyDropdownList(list.gameObject);

            UIRoot.SetActive(false);
        }

        private static float timeOfLastRaycast;

        public bool TryUpdate()
        {
            if (IInputManager.GetKeyDown(ConfigManager.World_MouseInspect_Keybind.Value))
                Instance.StartInspect(MouseInspectMode.World, inspectorAction);

            if (IInputManager.GetKeyDown(ConfigManager.UI_MouseInspect_Keybind.Value))
                Instance.StartInspect(MouseInspectMode.UI, inspectorAction);

            if (Inspecting)
                UpdateInspect();

            return Inspecting;
        }

        /// <summary>
        /// Updates the title text in the inspector UI, if the inspector title label is assigned.
        /// </summary>
        /// <param name="title">The new title text to display in the inspector.</param>
        internal void UpdateInspectorTitle(string title)
        {
            // Unity null check - if inspectorLabelTitle is assigned, update its text.
            if (inspectorLabelTitle)
            {
                inspectorLabelTitle.text = title;
            }
        }
        /// <summary>
        /// Updates the object name label in the inspector UI, if the label is assigned.
        /// </summary>
        /// <param name="name">The new object name to display.</param>
        internal void UpdateObjectNameLabel(string name)
        {
            // Unity null check - if objNameLabel is assigned, update its text.
            if (objNameLabel)
            {
                objNameLabel.text = name;
            }
        }
        /// <summary>
        /// Updates the object path label in the inspector UI, if the label is assigned.
        /// </summary>
        /// <param name="path">The new object path to display.</param>
        internal void UpdateObjectPathLabel(string path)
        {
            // Unity null check - if objPathLabel is assigned, update its text.
            if (objPathLabel)
            {
                objPathLabel.text = path;
            }
        }

        public void UpdateInspect()
        {
            if (IInputManager.GetKeyDown(KeyCode.Escape))
            {
                StopInspect();
                return;
            }

            if (IInputManager.GetMouseButtonDown(0))
            {
                CurrentInspector.OnSelectMouseInspect(inspectorAction);
                StopInspect();
                return;
            }

            Vector3 mousePos = IInputManager.MousePosition;
            if (mousePos != lastMousePos)
                UpdatePosition(mousePos);

            if (!timeOfLastRaycast.OccuredEarlierThan(0.1f))
                return;
            timeOfLastRaycast = Time.realtimeSinceStartup;

            CurrentInspector.UpdateMouseInspect(mousePos);
        }

        internal void UpdatePosition(Vector2 mousePos)
        {
            lastMousePos = mousePos;

            // use the raw mouse pos for the label
            mousePosLabel.text = $"<color=grey>Mouse Position:</color> {mousePos.x}, {mousePos.y}";

            // constrain the mouse pos we use within certain bounds
            if (mousePos.x < 350)
                mousePos.x = 350;
            if (mousePos.x > Screen.width - 350)
                mousePos.x = Screen.width - 350;
            if (mousePos.y < Rect.rect.height)
                mousePos.y += Rect.rect.height + 10;
            else
                mousePos.y -= 10;

            // calculate and set our UI position
            Vector3 inversePos = inspectorUIBase.RootObject.transform.InverseTransformPoint(mousePos);
            UIRoot.transform.localPosition = new Vector3(inversePos.x, inversePos.y, 0);
        }

        // UI Construction

        public override void SetDefaultSizeAndPosition()
        {
            base.SetDefaultSizeAndPosition();

            Rect.anchorMin = Vector2.zero;
            Rect.anchorMax = Vector2.zero;
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(700, 150);
        }

        protected override void ConstructPanelContent()
        {
            // hide title bar
            this.TitleBar.SetActive(false);
            this.UIRoot.transform.SetParent(UIManager.UIRoot.transform, false);

            GameObject inspectContent = UIFactory.CreateVerticalGroup(this.ContentRoot, "InspectContent", true, true, true, true, 3, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(inspectContent, flexibleWidth: 9999, flexibleHeight: 9999);

            // Title text

            inspectorLabelTitle = UIFactory.CreateLabel(inspectContent,
                "InspectLabel",
                "",
                TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(inspectorLabelTitle.gameObject, flexibleWidth: 9999);

            mousePosLabel = UIFactory.CreateLabel(inspectContent, "MousePosLabel", "Mouse Position:", TextAnchor.MiddleCenter);

            objNameLabel = UIFactory.CreateLabel(inspectContent, "HitLabelObj", "No hits...", TextAnchor.MiddleLeft);
            objNameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            objPathLabel = UIFactory.CreateLabel(inspectContent, "PathLabel", "", TextAnchor.MiddleLeft);
            objPathLabel.fontStyle = FontStyle.Italic;
            objPathLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            UIFactory.SetLayoutElement(objPathLabel.gameObject, minHeight: 75);

            UIRoot.SetActive(false);

            //// Create a new canvas for this panel to live on.
            //// It needs to always be shown on the main display, other panels can move displays.
            //
            //UIRoot.transform.SetParent(inspectorUIBase.RootObject.transform);
        }
    }
}
