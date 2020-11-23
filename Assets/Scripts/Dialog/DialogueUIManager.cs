﻿using System;
using System.Collections;
using System.Text;
using GameSystem.Save;
using UI;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Yarn;
using Yarn.Unity;

namespace Dialog
{
    /// <summary>
    /// Based on YarnSpinner's sample DialogueUI
    /// </summary>
    /// <seealso cref="DialogueUI"/>
    ///  todo: add more documentation
    ///  todo: add aliases in button options
    public class DialogueUIManager : Yarn.Unity.DialogueUIBehaviour, SaveClientCallback
    {
        [FormerlySerializedAs("assetManager")] public IconManager iconManager;

        [Header("Critical")] public GameConfiguration gameConfiguration;

        [Header("Prefabs")] [Tooltip("Prefab that contains the texts")]
        public GameObject dialogueItemPrefab;

        [Tooltip("Prefab that contains the portrait")]
        public GameObject dialoguePortraitPrefab;

        [Tooltip("Prefab for the options")] public GameObject dialogueOptionsPrefab;

        public String[] markupWholeWhitelist;
        public String[] markupPhraseWhitelist;
        
        private bool userRequestedNextLine = false;

        private System.Action<int> currentOptionSelectionHandler;

        private bool waitingForOptionSelection = false;

        public UnityEvent onDialogueStart;

        public UnityEvent onDialogueEnd;

        public UnityEvent onLinesStart;

        public UnityEvent onLineFinishDisplaying;

        public DialogueRunner.StringUnityEvent onLineUpdate;

        public UnityEvent onLineEnd;

        public UnityEvent onOptionsStart;

        public UnityEvent onOptionsEnd;

        public DialogueRunner.StringUnityEvent onCommand;

        public DialogueBlocker dialogueBlocker = new DialogueBlocker();

        private const int PoolSize = 10;
        private TextItem[] _textItems = new TextItem[PoolSize];
        private int _textIndex = 0;
        private int _portraitIndex = 0;
        private string _lastSpeaker = "";
        private bool requestDialogWrite = false;
        private SaveClient saveClient;
        private string _lastDialog;

        private float TextRate => gameConfiguration.textRate;

        public bool IsBlocking
        {
            get => dialogueBlocker.IsBlocking;
            set
            {
                if (value)
                {
                    dialogueBlocker.Block();
                }
                else
                {
                    dialogueBlocker.Unblock();
                }
            }
        }

        private void OnEnable()
        {
            saveClient = gameConfiguration.RequestSaveAccess(this);
        }

        private void OnDisable()
        {
            gameConfiguration.ReleaseSaveAccess(saveClient);
            saveClient = null;
        }

        private void Awake()
        {
            Debug.Assert(gameConfiguration != null);
            Debug.Assert(_textItems != null);
            Debug.Assert(iconManager != null);


            for (int i = 0; i < PoolSize; i++)
            {
                _textItems[i] = Instantiate(dialogueItemPrefab).GetComponent<TextItem>();
                _textItems[i].Activate();
            }
        }

        public override Dialogue.HandlerExecutionType RunLine(Line line, ILineLocalisationProvider localisationProvider,
            Action onLineComplete)
        {
            StartCoroutine(DoRunLine(line, localisationProvider, onLineComplete));
            return Dialogue.HandlerExecutionType.PauseExecution;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// For changing up the text. You have three places to watch out for:
        /// (1) When text is fed to the string slowly
        /// (2) When text was never fed to the string slowly
        /// (3) User decided to show the entire text immediately
        /// </remarks>
        /// <param name="line"></param>
        /// <param name="localisationProvider"></param>
        /// <param name="onComplete"></param>
        /// <returns></returns>
        private IEnumerator DoRunLine(Yarn.Line line, ILineLocalisationProvider localisationProvider,
            System.Action onComplete)
        {
            onLinesStart?.Invoke();

            userRequestedNextLine = false;

            string text = localisationProvider.GetLocalisedTextForLine(line);

            if (text == null)
            {
                Debug.LogWarning($"Line {line.ID} doesn't have any localised text.");
                text = line.ID;
            }

            // for empty //, we ignore them
            if (text.Trim().Equals("//"))
            {
                userRequestedNextLine = true;
                text = "";
            }

            // todo: deprecate
            // identifies speaker
            var argSplit = text.Split(':');
            InformSpeakerReturn speakerInfo = iconManager.InformSpeaker(argSplit.Length != 1 ? argSplit[0] : "");
            dialogueBlocker = speakerInfo.dialogueBlocker;
            var character = speakerInfo.character;

            if (speakerInfo.realName.Length != 0)
            {
                text = text.Replace($"{argSplit[0]}:", $"{speakerInfo.realName}:");
            }

            // onLineUpdate.AddListener(textItem.UpdateLine);

            while (dialogueBlocker.IsBlocking && !userRequestedNextLine)
            {
                yield return new WaitForSeconds(0.03f);
            }

            if (TextRate <= 0f)
            {
                gameConfiguration.textRate = 0f;
            }

            var stringBuilder = new StringBuilder();
            var markupBuilder = new StringBuilder();
            var textSpeedMultiplier = 1f;
            // todo: do something about markup symbols
            // todo: research text gui things that people may use
            // todo: change text speed

            foreach (var c in text)
            {
                #region for hiding markup

                if (markupBuilder.Length != 0)
                {
                    markupBuilder.Append(c);

                    if (c.Equals('>'))
                    {
                        AdjustMarkups(markupBuilder);

                        var markupText = markupBuilder.ToString().ToLower();
                        if (!gameConfiguration.enableTextFormatting  || !IsWhiteListed(markupText))
                        {
                            // don't do text formatting
                        }
                        else if (markupText.Contains("textspeed"))
                        {
                            var parseArgs = markupText.Split('=');
                            if (parseArgs.Length == 1)
                            {
                                textSpeedMultiplier = 1f;
                            }
                            else if (parseArgs.Length == 2 &&
                                     float.TryParse(parseArgs[1].Replace(">", "")
                                         .Replace("/", ""), out var tmpFloat))
                            {
                                textSpeedMultiplier = 1f / tmpFloat;
                            }
                            else
                            {
                                Debug.LogWarning($"Failed to translate markup: {markupText}");
                            }
                        }
                        else
                        {
                            stringBuilder.Append(markupBuilder);
                        }

                        markupBuilder.Clear();
                    }

                    continue;
                }

                if (c.Equals('<'))
                {
                    markupBuilder.Append(c);
                    continue;
                }

                #endregion for hiding markup

                stringBuilder.Append(c);
            }

            var formattedString = stringBuilder.ToString();
            var textLength = formattedString.Length;
            character.SetInitialText(formattedString);
            _lastDialog = formattedString;
            
            bool isSkipping = false;
            for (int i = 0; i < textLength; i++)
            {
                // onLineUpdate?.Invoke(stringBuilder.ToString());

                if (userRequestedNextLine)
                {
                    userRequestedNextLine = false;
                    break;
                }
                
                character.ShowCharacters(i);
                yield return new WaitForSeconds(TextRate * textSpeedMultiplier);
            }
            
            // i don't know why the last character's not shown sometimes
            character.ShowCharacters(textLength + 10);

            // Indicate to the rest of the game that the line has finished being delivered
            onLineFinishDisplaying?.Invoke();

            while (userRequestedNextLine == false)
            {
                yield return null;
            }

            userRequestedNextLine = false;

            // Avoid skipping lines if textSpeed == 0
            yield return new WaitForEndOfFrame();

            // Hide the text and prompt
            onLineEnd?.Invoke();

            // onLineUpdate.RemoveListener(textItem.UpdateLine);

            onComplete();
        }

        private bool IsWhiteListed(string markupText)
        {
            // todo: not in white list
            string markupLower = markupText.ToLower();
            foreach (var wholeMarkup in markupWholeWhitelist)
            {
                if (wholeMarkup.ToLower().Equals(markupLower))
                {
                    return true;
                }
            }

            foreach (var phraseMarkup in markupPhraseWhitelist)
            {
                if (markupLower.Contains(phraseMarkup.ToLower()))
                {
                    return true;
                }
            }
            
            Debug.Log($"Delete: {markupText}");
            
            return false;
        }

        private void AdjustMarkups(StringBuilder markupBuilder)
        {
            markupBuilder.Replace("<color=", "<color=#");
        }

        private string AdjustMarkups(String markupBuilder)
        {
            return markupBuilder.Replace("<color=", "<color=#");
        }

        public override void RunOptions(OptionSet optionSet, ILineLocalisationProvider localisationProvider,
            Action<int> onOptionSelected)
        {
            StartCoroutine(DoRunOptions(optionSet, localisationProvider, onOptionSelected));
        }


        /// Show a list of options, and wait for the player to make a
        /// selection.
        private IEnumerator DoRunOptions(Yarn.OptionSet optionsCollection,
            ILineLocalisationProvider localisationProvider,
            System.Action<int> selectOption)
        {
            iconManager.CreateButtons(optionsCollection.Options.Length);

            // Display each option in a button, and make it visible
            int i = 0;

            waitingForOptionSelection = true;

            currentOptionSelectionHandler = selectOption;

            foreach (var optionString in optionsCollection.Options)
            {
                iconManager.ActivateButtons(i, () => SelectOption(optionString.ID));

                var optionText = localisationProvider.GetLocalisedTextForLine(optionString.Line);

                if (optionText == null)
                {
                    Debug.LogWarning($"Option {optionString.Line.ID} doesn't have any localised text");
                    optionText = optionString.Line.ID;
                }

                iconManager.SetButtonText(i, optionText);

                i++;
            }

            onOptionsStart?.Invoke();
            Debug.Log("OptionStart");

            // Wait until the chooser has been used and then removed 
            while (waitingForOptionSelection)
            {
                yield return null;
            }

            Debug.Log("OptionEnd");
            iconManager.HideAllButtons();

            onOptionsEnd?.Invoke();
        }


        /// Runs a command.
        /// <inheritdoc/>
        public override Dialogue.HandlerExecutionType RunCommand(Yarn.Command command, System.Action onCommandComplete)
        {
            // Dispatch this command via the 'On Command' handler.
            onCommand?.Invoke(command.Text);

            // Signal to the DialogueRunner that it should continue
            // executing. (This implementation of RunCommand always signals
            // that execution should continue, and never calls
            // onCommandComplete.)
            return Dialogue.HandlerExecutionType.ContinueExecution;
        }


        /// Called when the dialogue system has started running.
        /// <inheritdoc/>
        public override void DialogueStarted()
        {
            // todo: Enable the dialogue controls.
            // if (dialogueContainer != null)
            //     dialogueContainer.SetActive(true);

            onDialogueStart?.Invoke();
        }

        /// Called when the dialogue system has finished running.
        /// <inheritdoc/>
        public override void DialogueComplete()
        {
            onDialogueEnd?.Invoke();

            // todo: Hide the dialogue interface.
            // if (dialogueContainer != null)
            //     dialogueContainer.SetActive(false);
        }

        /// <summary>
        /// Signals that the user has finished with a line, or wishes to
        /// skip to the end of the current line.
        /// </summary>
        /// <remarks>
        /// This method is generally called by a "continue" button, and
        /// causes the DialogueUI to signal the <see
        /// cref="DialogueRunner"/> to proceed to the next piece of
        /// content.
        ///
        /// If this method is called before the line has finished appearing
        /// (that is, before <see cref="onLineFinishDisplaying"/> is
        /// called), the DialogueUI immediately displays the entire line
        /// (via the <see cref="onLineUpdate"/> method), and then calls
        /// <see cref="onLineFinishDisplaying"/>.
        /// </remarks>
        public void MarkLineComplete()
        {
            userRequestedNextLine = true;
        }

        /// <summary>
        /// Signals that the user has selected an option.
        /// </summary>
        /// <remarks>
        /// This method is called by the <see cref="Button"/>s in the <see
        /// cref="optionButtons"/> list when clicked.
        ///
        /// If you prefer, you can also call this method directly.
        /// </remarks>
        /// <param name="optionID">The <see cref="OptionSet.Option.ID"/> of
        /// the <see cref="OptionSet.Option"/> that was selected.</param>
        public void SelectOption(int optionID)
        {
            // todo: check what this means
            if (waitingForOptionSelection == false)
            {
                Debug.LogWarning("An option was selected, but the dialogue UI was not expecting it.");
                return;
            }

            waitingForOptionSelection = false;
            currentOptionSelectionHandler?.Invoke(optionID);
        }

        public void RequestLastDialogWrite()
        {
            requestDialogWrite = true;
            saveClient.autoSave.lastDialog = "";
        }

        public void ShowElements(bool shouldShow)
        {
            foreach (var item in _textItems)
            {
                item.gameObject.SetActive(shouldShow);
            }

            iconManager.ShowElements(shouldShow);
        }

        public void WriteAutoSave()
        {
            saveClient.autoSave.lastDialog = _lastDialog;
        }
    }
}