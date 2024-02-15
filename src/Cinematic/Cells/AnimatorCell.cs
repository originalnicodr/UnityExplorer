﻿using UnityEngine;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

#if CPP
using Animator = UnityEngine.Behaviour;
#endif

namespace UnityExplorer.UI.Panels
{
    public class AnimatorCell : ICell
    {
        public Toggle IgnoreMasterToggle;
        public Toggle AnimatorToggle;

        public ButtonRef inspectButton;
        public Animator animator;

        // ICell
        public float DefaultHeight => 25f;
        public GameObject UIRoot { get; set; }
        public RectTransform Rect { get; set; }

        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);

// IL2CPP games seem to have animation-related code stripped from their builds
        private bool stopAfterAnimationFinishes = false;
#if MONO
        private bool skippedStopFrames;
        ButtonRef playButton;
        private Dropdown animatorDropdown;

        private AnimationClip currentAnimation;
        private AnimationClip defaultAnimation;

        public void DrawAnimatorPlayer(){
            if (playButton == null){
                List<AnimationClip> animations = animator.runtimeAnimatorController.animationClips.OrderBy(x=>x.name).Where(c => c.length > 0).Distinct().ToList();

                //ExplorerCore.LogWarning(animations.Count);
                AnimatorClipInfo[] playingAnimations = animator.GetCurrentAnimatorClipInfo(0);
                currentAnimation = playingAnimations.Count() != 0 ? playingAnimations[0].clip : animations[0];

                ButtonRef prevAnimation = UIFactory.CreateButton(UIRoot, "PreviousAnimation", "◀", new Color(0.05f, 0.05f, 0.05f));
                UIFactory.SetLayoutElement(prevAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
                prevAnimation.OnClick += () => animatorDropdown.value = animatorDropdown.value == 0 ? animatorDropdown.options.Count - 1 : animatorDropdown.value - 1;

                GameObject currentAnimationObj = UIFactory.CreateDropdown(UIRoot, $"Animations_{animator.name}", out animatorDropdown, null, 14, (idx) => currentAnimation = animations[idx]);
                UIFactory.SetLayoutElement(currentAnimationObj, minHeight: 25, minWidth: 200);
                foreach (AnimationClip animation in animations)
                    animatorDropdown.options.Add(new Dropdown.OptionData(animation.name));

                animatorDropdown.value = Math.Max(0, animations.FindIndex(a => a == currentAnimation));
                if (animatorDropdown.value == 0) animatorDropdown.captionText.text = animations[0].name;

                ButtonRef nextAnimation = UIFactory.CreateButton(UIRoot, "NextAnimation", "▶", new Color(0.05f, 0.05f, 0.05f));
                UIFactory.SetLayoutElement(nextAnimation.Component.gameObject, minHeight: 25, minWidth: 25);
                nextAnimation.OnClick += () => animatorDropdown.value = animatorDropdown.value == animatorDropdown.options.Count - 1 ? 0 : animatorDropdown.value + 1;

                playButton = UIFactory.CreateButton(UIRoot, "PlayButton", "Play", new Color(0.2f, 0.26f, 0.2f));
                UIFactory.SetLayoutElement(playButton.Component.gameObject, minHeight: 25, minWidth: 90);
                playButton.OnClick += PlayButton_OnClick;
            }
        }

        private void PlayButton_OnClick(){
            // We save the last animation played by the game in case we want to go back to it
            if (defaultAnimation == null){
                AnimatorClipInfo[] playingAnimations = animator.GetCurrentAnimatorClipInfo(0);
                defaultAnimation = playingAnimations.Count() != 0 ? playingAnimations[0].clip : null;
            }

            skippedStopFrames = false;

            stopAfterAnimationFinishes = !AnimatorToggle.isOn;
            AnimatorToggle.isOn = true;
            animator.Play(currentAnimation.name);
        }

        // Disables the animator when the animation we manually triggered isn't present on the subject anymore
        public bool IsPlayingSelectedAnimation(){
            if (animator != null && currentAnimation != null){
                if (stopAfterAnimationFinishes && !GetAllCurrentAnimations().Contains(currentAnimation)){
                    
                    // Wait a frame. Otherwise, it will stop the animation immediately.
                    if (!skippedStopFrames){
                        skippedStopFrames = true;
                        return false;
                    }

                    stopAfterAnimationFinishes = false;
                    AnimatorToggle.isOn = false;
                    return true;
                }
            }
            return false;
        }

        private List<AnimationClip> GetAllCurrentAnimations(){
            List<AnimationClip> allAnimations = new List<AnimationClip>();
            for (int layer = 0; layer < animator.layerCount; layer++){
                allAnimations.AddRange(animator.GetCurrentAnimatorClipInfo(layer).Select(ainfo => ainfo.clip).ToList());
            }
            return allAnimations;
        }

        public void ResetAnimation(){
            if (defaultAnimation != null){
                stopAfterAnimationFinishes = false;

                animator.Play(defaultAnimation.name);
                AnimatorToggle.isOn = true;
                defaultAnimation = null;
            }
        }
#endif

        public virtual GameObject CreateContent(GameObject parent)
        {
            GameObject AnimatorToggleObj = UIFactory.CreateToggle(parent, $"AnimatorToggle", out AnimatorToggle, out Text animatorToggleText);
            UIFactory.SetLayoutElement(AnimatorToggleObj, minHeight: 25);
            AnimatorToggle.isOn = true;
            AnimatorToggle.onValueChanged.AddListener(value => {
                    //ExplorerCore.LogWarning($"Animator toggled: {animator} to {animator.enabled}");
                    try {
                        Type animatorClass = ReflectionUtility.GetTypeByName("UnityEngine.Animator");
                        if (value) {
                            MethodInfo stopPlayBack = animatorClass.GetMethod("StopPlayback");
                            stopPlayBack.Invoke(animator.TryCast(), null);
                        } else {
                            MethodInfo startPlayBack = animatorClass.GetMethod("StartPlayback");
                            startPlayBack.Invoke(animator.TryCast(), null);
                        }
                    }
                    catch {
                        // Fallback in case reflection isn't working
                        animator.enabled = value;
                    }
                    // If we play an animation and we disable the animator then don't stop the animation when it finishes after we enable the animator again
                    if (!value && stopAfterAnimationFinishes) stopAfterAnimationFinishes = false;
                }
            );

            UIRoot = AnimatorToggleObj;
            UIRoot.SetActive(false);

            Rect = UIRoot.GetComponent<RectTransform>();
            Rect.anchorMin = new Vector2(0, 1);
            Rect.anchorMax = new Vector2(0, 1);
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(25, 25);
            
            UIFactory.SetLayoutElement(UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);

            inspectButton = UIFactory.CreateButton(UIRoot, "InspectButton", "");
            UIFactory.SetLayoutElement(inspectButton.GameObject, minWidth: 25, minHeight: 25, flexibleWidth: 9999);
            inspectButton.OnClick += () => InspectorManager.Inspect(animator.gameObject);
#if MONO
            ButtonRef resetAnimation = UIFactory.CreateButton(UIRoot, "Reset Animation", "Reset");
            UIFactory.SetLayoutElement(resetAnimation.GameObject, minWidth: 50, minHeight: 25);
            resetAnimation.OnClick += ResetAnimation;
#endif

            GameObject ignoresMasterTogglerObj = UIFactory.CreateToggle(UIRoot, $"AnimatorIgnoreMasterToggle", out IgnoreMasterToggle, out Text ignoreMasterToggleText);
            UIFactory.SetLayoutElement(ignoresMasterTogglerObj, minHeight: 25);
            IgnoreMasterToggle.isOn = false;
            IgnoreMasterToggle.onValueChanged.AddListener(IgnoreMasterToggle_Clicked);
            ignoreMasterToggleText.text = "Ignore Master Toggle  ";

            return UIRoot;
        }

        internal void IgnoreMasterToggle_Clicked(bool value){
            GetAnimatorPanel().shouldIgnoreMasterToggle[animator] = value;
        }

        public UnityExplorer.UI.Panels.AnimatorPanel GetAnimatorPanel(){
            return UIManager.GetPanel<UnityExplorer.UI.Panels.AnimatorPanel>(UIManager.Panels.AnimatorPanel);
        }
    }
}