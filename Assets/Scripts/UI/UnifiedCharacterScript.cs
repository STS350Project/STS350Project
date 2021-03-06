using System;
using System.Collections.Generic;
using Dialog;
using Others;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace UI
{
    /// <summary>
    /// This is two finite state machines which dictates how each character,
    /// and text associated with the character, appears in the scene.
    /// </summary>
    public class UnifiedCharacterScript : MonoBehaviour
    {
        [Header("Data")] public CharacterData characterData;
        public SpriteEmote defaultSprite;
        public SpriteEmote[] alternativeSprites;

        [Header("Locations")] public Transform stageLocation;
        public Transform exitLocation;

        [FormerlySerializedAs("alternativeTextLocation")]
        public Transform alternativeTextTransform;

        public AlternativeTextLocationData[] defaultTextLocationList;
        public AlternativeTextLocationData[] alternativeTextLocationList;
        public AlternativeTextLocationData[] alternativeStageLocationList;

        [Header("Child components")] public SpriteRenderer sprite;
        public TextMeshProUGUI dialogueText;
        public Transform layoutGroup;
        public GameObject uiButton;
        public Canvas textCanvas;
        public Canvas buttonCanvas;
        public Vector3 offsetIncrement = new Vector3(1f, -1f);
        public float transitionSpeed = 10f;
        public float textTransitionSpeed = 20f;
        public float inactiveZ = 1f;
        public float speakFloatHeight = 0.25f;
        public float speakFloatSpeed = 2f;

        [FormerlySerializedAs("_state")] public CharacterState characterState = CharacterState.Hidden;
        private CharacterState _previousState = CharacterState.Hidden;
        private TextState _textState = TextState.Default;
        private CharacterType _characterType = CharacterType.Narrator;
        private bool _isSpeaking = false;
        private int _currentTextMax;
        private IconManager _iconManager;
        private List<SpecialButton> _buttons = new List<SpecialButton>();
        private Vector3 _initialTextLocation;
        private Vector3 _idleTargetPosition;
        private float _timeOffset;
        private string _description = "";
        private string _oldDescription = "";
        private TextAlternativeLocationState _textAlternative = TextAlternativeLocationState.Default;
        private TextAlternativeLocationState _previousTextAlternative = TextAlternativeLocationState.Default;
        private Vector3 _alternativeTextLocation;
        private Vector3 _defaultTextLocation;
        private TextAlternativeLocationState _jojoPreviousAltState;
        private Vector3 _currentStagePosition;

        public string RealName => characterData.name;
        public CharacterType CharacterType => _characterType;

        private enum TextState
        {
            Default,
            ToAlternative,
            Alternative,
            ToDefault
        }

        private void Awake()
        {
            Debug.Assert(stageLocation != null);
            Debug.Assert(exitLocation != null);
            Debug.Assert(sprite != null);
        }

        private void Start()
        {
            _alternativeTextLocation = alternativeTextTransform.position;
            _initialTextLocation = dialogueText.transform.position;
            _defaultTextLocation = _initialTextLocation;
            _currentStagePosition = stageLocation.position;

            dialogueText.text = "";

            if (buttonCanvas != null)
            {
                Camera _camera = Camera.main;
                buttonCanvas.worldCamera = _camera;
            }
        }

        private void Update()
        {
            if (characterState != _previousState)
            {
                RefreshSprite();

                _previousState = characterState;
            }

            switch (characterState)
            {
                case CharacterState.Hidden:
                    // do nothing
                    break;
                case CharacterState.Appearing:
                    Appear();
                    break;
                case CharacterState.Idling:
                    if (_isSpeaking)
                    {
                        characterState = CharacterState.Speaking;
                        _timeOffset = Time.time;
                        _iconManager.informSpeakerReturnValue.dialogueBlocker.Unblock();
                    }
                    else
                    {
                        sprite.transform.position = _currentStagePosition;
                    }

                    break;
                case CharacterState.Speaking:
                    sprite.transform.position = _currentStagePosition +
                                                (Mathf.Abs(Mathf.Sin((Time.time - _timeOffset) *
                                                                     speakFloatSpeed)) * speakFloatHeight * Vector3.up);
                    break;
                case CharacterState.Disappearing:
                    Disappear();
                    break;
                case CharacterState.ToRepositionIdling:
                    if (sprite.transform.position != _idleTargetPosition)
                    {
                        sprite.transform.position = Vector3.MoveTowards(sprite.transform.position,
                            _idleTargetPosition, textTransitionSpeed * Time.deltaTime);
                    }
                    else
                    {
                        characterState = CharacterState.RepostitionIdling;
                    }

                    break;
                case CharacterState.RepostitionIdling:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (_textAlternative != _previousTextAlternative)
            {
                _alternativeTextLocation = alternativeTextTransform.position;

                foreach (var data in alternativeTextLocationList)
                {
                    if (data.state == _textAlternative)
                    {
                        _alternativeTextLocation = data.transform.position;
                        break;
                    }
                }

                _defaultTextLocation = _initialTextLocation;

                foreach (var data in defaultTextLocationList)
                {
                    if (data.state == _textAlternative)
                    {
                        _defaultTextLocation = data.transform.position;
                        break;
                    }
                }

                _previousTextAlternative = _textAlternative;

                _currentStagePosition = stageLocation.position;

                foreach (var data in alternativeStageLocationList)
                {
                    if (data.state == _textAlternative)
                    {
                        _currentStagePosition = data.transform.position;
                        break;
                    }
                }

                switch (_textState)
                {
                    case TextState.Default:
                        _textState = TextState.ToDefault;
                        break;
                    case TextState.ToAlternative:
                        break;
                    case TextState.Alternative:
                        _textState = TextState.Alternative;
                        break;
                    case TextState.ToDefault:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            switch (_textState)
            {
                case TextState.Default:
                    break;
                case TextState.ToAlternative:
                    if (dialogueText.transform.position != _alternativeTextLocation)
                    {
                        dialogueText.transform.position = Vector3.MoveTowards(dialogueText.transform.position,
                            _alternativeTextLocation, textTransitionSpeed * Time.deltaTime);
                    }
                    else
                    {
                        _textState = TextState.Alternative;
                    }

                    break;
                case TextState.Alternative:
                    break;
                case TextState.ToDefault:
                    if (dialogueText.transform.position != _defaultTextLocation)
                    {
                        dialogueText.transform.position = Vector3.MoveTowards(dialogueText.transform.position,
                            _defaultTextLocation, textTransitionSpeed * Time.deltaTime);
                    }
                    else
                    {
                        _textState = TextState.Default;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Appear()
        {
            switch (_characterType)
            {
                case CharacterType.Narrator:
                    // do nothing
                    break;
                case CharacterType.Main:
                case CharacterType.Side:
                    sprite.transform.position = Vector3.MoveTowards(sprite.transform.position,
                        _currentStagePosition,
                        transitionSpeed * Time.deltaTime);
                    if (sprite.transform.position == _currentStagePosition)
                    {
                        characterState = CharacterState.Idling;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Disappear()
        {
            switch (_characterType)
            {
                case CharacterType.Narrator:
                    // do nothing
                    break;
                case CharacterType.Main:
                case CharacterType.Side:
                    sprite.transform.position = Vector3.MoveTowards(sprite.transform.position,
                        exitLocation.position,
                        transitionSpeed * Time.deltaTime);
                    if (sprite.transform.position == exitLocation.position)
                    {
                        characterState = CharacterState.Hidden;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Initializes unified character script
        /// </summary>
        /// <param name="iconManager"></param>
        public void SetData(IconManager iconManager)
        {
            _iconManager = iconManager;
            _characterType = characterData.characterType;
        }

        /// <summary>
        /// Returns true if the candidate speaker matches their name or alias
        /// </summary>
        /// <param name="candidateSpeaker"></param>
        /// <returns></returns>
        public bool IsSimilar(string candidateSpeaker)
        {
            return characterData.IsSimilar(candidateSpeaker);
        }

        /// <summary>
        /// Updates the status of a character depending on their emotion or description.
        /// It also checks what their status is based on icon manager.
        /// </summary>
        /// <param name="description"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void UpdateStatus(string description)
        {
            if (_iconManager.currentSpeaking != this && characterState != CharacterState.Hidden)
            {
                characterState = CharacterState.Idling;
                _isSpeaking = false;
            }
            else if (_iconManager.currentSpeaking == this && _characterType != CharacterType.Narrator)
            {
                _isSpeaking = true;
                switch (characterState)
                {
                    case CharacterState.Hidden:
                        // show up
                        characterState = CharacterState.Appearing;
                        _iconManager.informSpeakerReturnValue.dialogueBlocker.Block();
                        break;
                    case CharacterState.Appearing:
                        _iconManager.informSpeakerReturnValue.dialogueBlocker.Block();
                        // just keep it up
                        break;
                    case CharacterState.Idling:
                        // go to speak immediately
                        characterState = CharacterState.Speaking;
                        _timeOffset = Time.time;
                        break;
                    case CharacterState.Speaking:
                        // keep the state
                        break;
                    case CharacterState.ToRepositionIdling:
                        // go to speak immediately
                        characterState = CharacterState.Speaking;
                        _timeOffset = Time.time;
                        break;
                    case CharacterState.Disappearing:
                        Debug.LogWarning("UpdateStatus: This should not happen");
                        break;
                    case CharacterState.RepostitionIdling:
                        characterState = CharacterState.Appearing;
                        _iconManager.informSpeakerReturnValue.dialogueBlocker.Block();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (_characterType == CharacterType.Side && !_isSpeaking && characterState != CharacterState.Hidden)
            {
                // check index in active characters
                int currentIndex = _iconManager.GetSideCharacterIndex(this);
                Debug.Assert(currentIndex != -1);
                _idleTargetPosition = _currentStagePosition + (currentIndex * offsetIncrement);
                _idleTargetPosition.z = inactiveZ;
                characterState = CharacterState.ToRepositionIdling;
            }

            if (characterState == CharacterState.Idling)
            {
                characterState = CharacterState.Appearing;
            }


            if (_isSpeaking)
            {
                _description = description.Trim();
                if (!_description.Equals(_oldDescription))
                {
                    // Ver bad hardcoding T.T
                    if (_oldDescription.ToLower().Equals("jojo"))
                    {
                        _iconManager.UpdateAlternativeTextLocation(_jojoPreviousAltState);
                    }

                    if (_description.ToLower().Equals("jojo"))
                    {
                        _jojoPreviousAltState = _textAlternative;
                        _iconManager.UpdateAlternativeTextLocation(TextAlternativeLocationState.Jojo);
                    }

                    _oldDescription = _description;
                    RefreshSprite();
                }
            }

            textCanvas.gameObject.SetActive(false);
            dialogueText.text = "";
        }

        /// <summary>
        /// Refreshes the sprite based on their current description and state.
        /// </summary>
        private void RefreshSprite()
        {
            // update sprite based on description + state
            var bestSpriteData = defaultSprite;
            foreach (var spriteData in alternativeSprites)
            {
                if (spriteData.IsSimilar(_description))
                {
                    bestSpriteData = spriteData;
                    break;
                }
            }

            bestSpriteData.GetBestSprite(sprite, characterState);
        }

        /// <summary>
        /// Sets the text of the text associated with the character.
        /// It also sets the visible characters to zero.
        /// </summary>
        /// <param name="text"></param>
        public void SetInitialText(string text)
        {
            textCanvas.gameObject.SetActive(true);
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.text = text;
            _currentTextMax = text.Length;
        }

        /// <summary>
        /// Sets the visible characters of the script.
        /// </summary>
        /// <param name="count"></param>
        public void ShowCharacters(int count)
        {
            dialogueText.maxVisibleCharacters = count;
        }

        /// <summary>
        /// Creates the buttons, given the optionsLength, for the main character.
        /// Only use for the main character.
        /// This will not make uneccessary buttons.
        /// </summary>
        /// <param name="optionsLength"></param>
        public void CreateButtons(int optionsLength)
        {
            Debug.Assert(_characterType == CharacterType.Main);
            buttonCanvas.gameObject.SetActive(true);

            while (_buttons.Count < optionsLength)
            {
                var newButton = Instantiate(uiButton, layoutGroup).GetComponent<SpecialButton>();
                newButton.gameObject.SetActive(false);
                _buttons.Add(newButton);
            }

            // move text to alternative location
            _textState = TextState.ToAlternative;
        }

        /// <summary>
        /// Activates the buttons to make them appear.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="call"></param>
        public void ActivateButtons(int i, UnityAction call)
        {
            Debug.Assert(_characterType == CharacterType.Main);
            Debug.Assert(i < _buttons.Count);
            _buttons[i].gameObject.SetActive(true);
            _buttons[i].onClick.RemoveAllListeners();
            _buttons[i].onClick.AddListener(call);
        }

        /// <summary>
        /// Sets the text of button on index i with string optionText
        /// </summary>
        /// <param name="i"></param>
        /// <param name="optionText"></param>
        public void SetButtonText(int i, string optionText)
        {
            _buttons[i].SetText(optionText);
        }

        /// <summary>
        /// Makes the button disappear
        /// </summary>
        public void HideAllButtons()
        {
            foreach (var button in _buttons)
            {
                button.gameObject.SetActive(false);
            }

            buttonCanvas.gameObject.SetActive(false);

            // move text to old location
            _textState = TextState.ToDefault;
        }

        /// <summary>
        /// Makes the character leave the visible scene.
        /// </summary>
        public void Leave()
        {
            characterState = CharacterState.Disappearing;
            dialogueText.text = "";
        }

        /// <summary>
        /// Forces the text location to its alternative location. This is useful when dialogue options are being shown.
        /// </summary>
        public void SetTextAlternativeLocation()
        {
            _textState = TextState.ToAlternative;
        }

        /// <summary>
        /// Resets the text location. This is useful after a dialogue option was chosen
        /// </summary>
        public void ResetTextLocation()
        {
            _textState = TextState.ToDefault;
        }

        /// <summary>
        /// For certain scenes, the text locations may need to change. Use this to communicate that.
        /// </summary>
        /// <param name="alternativeLocationState"></param>
        public void SetAlternativeLocationState(TextAlternativeLocationState alternativeLocationState)
        {
            _textAlternative = alternativeLocationState;
        }
    }

    public enum CharacterState
    {
        Hidden,
        Appearing,
        Idling,
        Speaking,
        Disappearing,
        ToRepositionIdling,
        RepostitionIdling
    }

    [Serializable]
    public class CharacterData : DataItem
    {
        public CharacterType characterType = CharacterType.Side;
    }

    [Serializable]
    public class SpriteEmote : DataItem
    {
        public SpriteSubItem defaultState;
        public SpriteSubItem[] alternativeStates;

        public void GetBestSprite(SpriteRenderer spriteRenderer, CharacterState characterState)
        {
            var bestState = defaultState;
            foreach (var state in alternativeStates)
            {
                if (state.characterState == characterState)
                {
                    bestState = state;
                    break;
                }
            }

            spriteRenderer.color = bestState.color;
            spriteRenderer.sprite = bestState.sprite;
        }
    }

    [Serializable]
    public class SpriteSubItem
    {
        public Sprite sprite;
        public CharacterState characterState;
        public Color color = Color.white;
    }

    public enum CharacterType
    {
        Narrator,
        Main,
        Side
    }

    public enum TextAlternativeLocationState
    {
        Default,
        Sunset,
        Jojo
    }

    [Serializable]
    public class AlternativeTextLocationData
    {
        public Transform transform;
        public TextAlternativeLocationState state = TextAlternativeLocationState.Sunset;
    }
}