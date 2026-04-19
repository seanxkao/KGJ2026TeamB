using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BGMManager : MonoBehaviour
{
    [Serializable]
    public class BGMInfo
    {
        public string Id;
        public AudioClip Clip;
    }

    [SerializeField]
    private AudioSource _audioSource;
    [SerializeField]
    public List<BGMInfo> _bgmInfos;

    public string _currentId = string.Empty;

    public AudioSource AudioSource => _audioSource;

    public void Play(string id)
    {
        var info = _bgmInfos.Find(i => i.Id == id);
        if (info == null)
        {
            Debug.LogWarning($"Cannot find bgm <{id}>");
            return;
        }

        if (id == _currentId)
        {
            return;
        }

        _currentId = id;
        _audioSource.Stop();
        _audioSource.clip = info.Clip;
        _audioSource.Play();
    }
}
