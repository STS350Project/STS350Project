﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using Gameplay;
using GameSystem.Save;
using Others;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Dialog
{
    public class IconManager : MonoBehaviour, SaveClientCallback
    {
        public static string mainSpeakerName = "Pengi";
    
        public GameConfiguration gameConfiguration;
        public GameObject[] characterPrefabList;
        public GameObject mainCharacterPrefab;
        public GameObject narratingCharacterPrefab;
        public InformSpeakerReturn informSpeakerReturnValue = new InformSpeakerReturn();

        private SaveClient _saveClient;
        private UnifiedCharacterScript[] _characterList;
        private UnifiedCharacterScript _mainCharacter;
        private UnifiedCharacterScript _narratingCharacter;
        private List<UnifiedCharacterScript> _activeCharacterList = new List<UnifiedCharacterScript>();
        public UnifiedCharacterScript currentSpeaking;

        private void OnEnable()
        {
            if (_saveClient == null)
            {
                _saveClient = gameConfiguration.RequestSaveAccess(this);
            }
        }

        private void OnDisable()
        {
            gameConfiguration.ReleaseSaveAccess(_saveClient);
            _saveClient = null;
        }

        private void Awake()
        {
            Debug.Assert(gameConfiguration != null);
        
            _characterList = new UnifiedCharacterScript[characterPrefabList.Length];
            for (int i = 0; i < characterPrefabList.Length; i++)
            {
                _characterList[i] = InstantiateCharacter(characterPrefabList[i]);
            }

            _narratingCharacter = InstantiateCharacter(narratingCharacterPrefab);
            _mainCharacter = InstantiateCharacter(mainCharacterPrefab);

            if (_saveClient == null)
            {
                _saveClient = gameConfiguration.RequestSaveAccess(this);
            }

            InformSpeaker(_saveClient.currentSave.currentSpeaker, true);
            InformSpeaker(_saveClient.currentSave.previousSpeaker, true);
        }
    
        public UnifiedCharacterScript InstantiateCharacter(GameObject prefab)
        {
            var script = Object.Instantiate(prefab)
                .GetComponent<UnifiedCharacterScript>();
            Debug.Assert(script != null);
            script.SetData(this);
            return script;
        }

        public void RemoveSpeaker(int count)
        {
            Debug.LogError("RemoveSpeaker not implemented");
        }

        public void RemoveSpeaker(string speakerName)
        {
            Debug.LogError("RemoveSpeaker not implemented");
        }

        public InformSpeakerReturn InformSpeaker(string candidateSpeaker)
        {
            return InformSpeaker(candidateSpeaker, false);
        }

        private InformSpeakerReturn InformSpeaker(string candidateSpeaker, 
            bool isForced)
        {    
            // find out who is the active speaker
            // arrange accordingly
            bool shouldRearrange = false;
            
            // extract description
            var speakingParts = candidateSpeaker.Split(',');
            candidateSpeaker = speakingParts[0];
            var description = speakingParts.Length != 2 ? "" : speakingParts[1];
        
            if (_narratingCharacter.IsSimilar(candidateSpeaker))
            {
                currentSpeaking = _narratingCharacter;
            } else if (_mainCharacter.IsSimilar(candidateSpeaker))
            {
                currentSpeaking = _mainCharacter;
            }
            else
            {
                foreach (var character in _activeCharacterList)
                {
                    if (character.IsSimilar(candidateSpeaker))
                    {
                        currentSpeaking = character;
                        shouldRearrange = true;
                        break;
                    }
                }
            }

            if (shouldRearrange)
            {
                _activeCharacterList.Remove(currentSpeaking);
                _activeCharacterList.Insert(0, currentSpeaking);
            }

            _narratingCharacter.UpdateStatus(description);
            _mainCharacter.UpdateStatus(description);

            foreach (var character in _activeCharacterList)
            {
                character.UpdateStatus(description);
            }

            informSpeakerReturnValue.character = currentSpeaking;
            informSpeakerReturnValue.realName = currentSpeaking.RealName;

            return informSpeakerReturnValue;
        
            /*var ret = new InformSpeakerReturn();
        candidateSpeaker = candidateSpeaker.Trim();

        if (candidateSpeaker.Equals(""))
        {
            // do nothing ??
            Speak(defaultCharacter);
            ret.isBlocking = false;
            return SaveState(ret);
        }

        if (_mainSpeaker != null && _mainSpeaker.IsSameSpeaker(candidateSpeaker))
        {
            ret.realName = _mainSpeaker.GetRealName();
            Speak(_mainSpeaker);
            return SaveState(ret);
        }

        if (_otherSpeaker != null && _otherSpeaker.IsSameSpeaker(candidateSpeaker))
        {
            ret.realName = _otherSpeaker.GetRealName();
            Speak(_otherSpeaker);
            return SaveState(ret);
        }

        // did not match current speakers
        var currentSpeaker = GetSpeakerPortrait(candidateSpeaker);
        if (currentSpeaker.IsSameSpeaker(mainSpeakerName))
        {
            // we know that main speaker is null
            _mainSpeaker = currentSpeaker;
            Speak(_mainSpeaker); // must be in here
            ret.isBlocking = !isForced;
            ret.realName = _mainSpeaker.GetRealName();
            return SaveState(ret);
        }

        if (_otherSpeaker != null)
        {
            // we have replace the other speaker
            _otherSpeaker.Leave();
        }

        _otherSpeaker = currentSpeaker;
        Speak(_otherSpeaker); // must be here
        ret.isBlocking = !isForced;
        ret.realName = _otherSpeaker.GetRealName();
        return SaveState(ret);*/
        }

// todo: delete
        private InformSpeakerReturn SaveState(InformSpeakerReturn ret)
        {
            return ret;
        }

        public void ShowElements(bool shouldShow)
        {
            foreach (var characterScript in _characterList)
            {
                characterScript.gameObject.SetActive(shouldShow);
            }
        
            _mainCharacter.gameObject.SetActive(shouldShow);
            _narratingCharacter.gameObject.SetActive(shouldShow);
        }

        public void WriteAutoSave()
        {
            Debug.LogError("WriteAutoSave not implemented");
            // _saveClient.autoSave.isLeft = !_isLeft;
            // _saveClient.autoSave.currentSpeaker = _otherSpeaker != null
            //     ? _otherSpeaker.Speaker
            //     : "";
            // _saveClient.autoSave.previousSpeaker = _mainSpeaker != null
            //     ? _mainSpeaker.Speaker
            //     : "";
        }

        public void EnterStage(string characterName)
        {
            foreach (var characterScript in _characterList)
            {
                if (characterScript.IsSimilar(characterName))
                {
                    _activeCharacterList.Add(characterScript);
                    return;
                }
            }
        
            Debug.LogWarning($"EnterStage: Character {characterName} not in character list");
        }

        public void ExitStage(string characterName)
        {
            for (int i = 0; i < _activeCharacterList.Count; i--)
            {
                if (_activeCharacterList[i].IsSimilar(characterName))
                {
                    _activeCharacterList[i].Leave();
                    _activeCharacterList.RemoveAt(i);
                    return;
                }
            }
        
            Debug.LogWarning($"ExitStage: Character {characterName} not in character list");
        }

        public void CreateButtons(int optionsLength)
        {
            _mainCharacter.CreateButtons(optionsLength);
        }

        public void ActivateButtons(int i, UnityAction action)
        {
            _mainCharacter.ActivateButtons(i, action);
        }

        public void SetButtonText(int i, string optionText)
        {
            _mainCharacter.SetButtonText(i, optionText);
        }

        public void HideAllButtons()
        {
            _mainCharacter.HideAllButtons();
        }

        public int GetSideCharacterIndex(UnifiedCharacterScript unifiedCharacterScript)
        {
            return _activeCharacterList.IndexOf(unifiedCharacterScript);
        }
    }

    [Serializable]
    public class IconItem : DataItem
    {
        [FormerlySerializedAs("sprite")] public Sprite mainSprite;
        public Sprite outlineSprite;
    }

    public class InformSpeakerReturn
    {
        public UnifiedCharacterScript character;
        public readonly DialogueBlocker dialogueBlocker = new DialogueBlocker();
        public string realName = "";
    }

    public class DialogueBlocker
    {
        private bool _isBlocking = false;
        public bool IsBlocking => _isBlocking;

        public void Unblock()
        {
            _isBlocking = false;
        }

        public void Block()
        {
            _isBlocking = true;
        }
    }
}