using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GPUAnimator.Baker;

namespace GPUAnimator.Player
{
    public class AnimationBakerMenu
    {

        [MenuItem("GameObject/AnimationBaker/Bake2Tex", false, 10)]
        private static void BakeAnimation(MenuCommand menuCommand)
        {
            var targetObject = menuCommand.context as GameObject;

            AnimationTextureBaker baker = targetObject.GetComponent<AnimationTextureBaker>();

            if (baker == null)
                baker = targetObject.AddComponent<AnimationTextureBaker>();
            baker.LegacyBakeAll();

        }
    }
}
