using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUAnimator.Player
{
    public class TextureAnimations
    {
        private BakedTextureAnimation[] _animations;
        private Dictionary<string, int> _nameToHashDict;
        private Dictionary<int, BakedTextureAnimation> _animationsDict;
        private Dictionary<int, BakedTextureExtraInfo> _animationsExtraDict;

        public TextureAnimations(BakedTextureAnimation[] animations)
        {
            _animations = animations;
            _animationsDict = new Dictionary<int, BakedTextureAnimation>();
            _nameToHashDict = new Dictionary<string, int>();
            _animationsExtraDict = new Dictionary<int, BakedTextureExtraInfo>();
            for (int i = 0; i < _animations.Length; i++)
            {
                var anim = _animations[i];

                BakedTextureExtraInfo bakedTextureExtraInfo = new BakedTextureExtraInfo();
                int shortNameHash = Animator.StringToHash(anim.animationName);
                bakedTextureExtraInfo.texelSize = new Vector4(
                    1f / anim.positionAnimTexture.width, 1f / anim.positionAnimTexture.height,
                    anim.positionAnimTexture.width, anim.positionAnimTexture.height);
                bakedTextureExtraInfo.shortNameHash = shortNameHash;

                _nameToHashDict.Add(anim.animationName, shortNameHash);
                _animationsDict.Add(shortNameHash, anim);
                _animationsExtraDict.Add(shortNameHash, bakedTextureExtraInfo);
            }
        }

        public int GetShortNameHash(string name)
        {
            if (_nameToHashDict.ContainsKey(name))
            {
                return _nameToHashDict[name];
            }
            else
            {
                return -1;
            }
        }

        public BakedTextureAnimation Find(int hash)
        {
            if (_animationsDict.ContainsKey(hash))
            {
                return _animationsDict[hash];
            }
            else
            {
                return null;
            }
        }

        public BakedTextureExtraInfo GetBakedTextureExtraInfo(string animName)
        {
            int shortNameHash = this.GetShortNameHash(animName);
            if (shortNameHash == -1) { return null; }
            return this.GetBakedTextureExtraInfo(shortNameHash);
        }
        
        public BakedTextureExtraInfo GetBakedTextureExtraInfo(int hash)
        {
            if (_animationsExtraDict.ContainsKey(hash))
            {
                return _animationsExtraDict[hash];
            }
            else
            {
                return null;
            }
        }
    }

    [System.Serializable]
    public class BakedTextureAnimation
    {
        //public int fullPathHash;
        public string animationName;
        public Texture positionAnimTexture;
        public Texture normalAnimTexture;
        //public Vector4 texelSize;
    }
    [System.Serializable]
    public class BakedTextureExtraInfo
    {
        public int shortNameHash;
        public Vector4 texelSize;
    }

}