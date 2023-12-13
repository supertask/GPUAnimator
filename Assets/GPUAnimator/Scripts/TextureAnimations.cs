using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUAnimator.Player
{
    public class TextureAnimations : MonoBehaviour
    {

        [SerializeField]
        BakedTextureAnimation[] animations;


        public BakedTextureAnimation[] Animations
        {
            get
            {
                return animations;
            }
        }

        private Dictionary<int, BakedTextureAnimation> animationsDict;
        private Dictionary<string, int> nameToHashDict;

        public void Init()
        {
            animationsDict = new Dictionary<int, BakedTextureAnimation>();
            nameToHashDict = new Dictionary<string, int>();
            if (animations == null) { return; }
            foreach( var anim in animations)
            {
                int shortNameHash = Animator.StringToHash(anim.animationName);
                animationsDict.Add(shortNameHash, anim);
                nameToHashDict.Add(anim.animationName, shortNameHash);
            }
        }

        public int GetShortNameHash(string name)
        {
            if (nameToHashDict.ContainsKey(name))
            {
                return nameToHashDict[name];
            }
            else
            {
                return -1;
            }
        }

        public BakedTextureAnimation Find(int hash)
        {
            if (animationsDict.ContainsKey(hash))
            {
                return animationsDict[hash];
            }
            else
            {
                return null;
            }
        }

        public void SetItemSource(List<BakedTextureAnimation> bakedTextureAnimations)
        {
            animations = bakedTextureAnimations.ToArray();
        }

        //   // Use this for initialization
        //   void Start () {

        //}

        //// Update is called once per frame
        //void Update () {

        //}
    }

    [System.Serializable]
    public class BakedTextureAnimation
    {
        public int fullPathHash;
        public string animationName;
        public Texture positionAnimTexture;
        public Texture normalAnimTexture;
        public Vector4 texelSize;
    }
}