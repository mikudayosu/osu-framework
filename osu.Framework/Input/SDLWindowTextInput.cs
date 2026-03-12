// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Platform;
using osu.Framework.Logging;

namespace osu.Framework.Input
{
    internal class SDLWindowTextInput : TextInputSource
    {
        private readonly ISDLWindow window;

        private Action? pendingTextInput;
        private bool hadActiveComposition;
        private bool pendingEmptyEditing;

        public SDLWindowTextInput(ISDLWindow window)
        {
            this.window = window;
        }

        private void handleTextInput(string text)
        {
            Logger.Log($"[IME] handleTextInput: '{text}', hadActiveComposition={hadActiveComposition}");
            
            if (hadActiveComposition || pendingEmptyEditing)
            {
                pendingEmptyEditing = false;
                hadActiveComposition = false;
                pendingTextInput = () => TriggerImeResult(text);
            }
            else
            {
                TriggerTextInput(text);
            }
        }

        private void handleTextEditing(string? text, int selectionStart, int selectionLength)
        {
            if (text == null) return;

            if (!string.IsNullOrEmpty(text))
            {
                // A new composition has started.
                if (pendingEmptyEditing)
                {
                    // Next composition arrived without a TextInput event,
                    // meaning the previous character was committed without going through the pending path.
                    pendingEmptyEditing = false;
                    hadActiveComposition = false;
                }
                
                if (pendingTextInput != null)
                {
                    // Flush the previous pending commit before starting a new composition.
                    Logger.Log($"[IME] Invoking pendingTextInput before new composition");
                    pendingTextInput.Invoke();
                    pendingTextInput = null;
                }
                
                hadActiveComposition = true;
                TriggerImeComposition(text, selectionStart, selectionLength);
            }
            else
            {
                // Empty TextEditing received.
                if (pendingTextInput != null)
                {
                    // Normal path: TextInput arrived before TextEditing(""), flush it now.
                    Logger.Log($"[IME] Invoking pendingTextInput");
                    pendingTextInput.Invoke();
                    pendingTextInput = null;
                    hadActiveComposition = false;
                    TriggerImeComposition(text, selectionStart, selectionLength);
                }
                else if (hadActiveComposition)
                {
                    // fcitx5 path: TextEditing("") arrives before TextInput.
                    // Defer the empty composition event and wait for the TextInput to arrive.
                    pendingEmptyEditing = true;
                    // Do not call TriggerImeComposition here — wait for TextInput first.
                }
                else
                {
                    // Empty composition with no active composition = IME reset/initialisation event.
                    TriggerImeComposition(text, selectionStart, selectionLength);
                }
            }
        }

        protected override void ActivateTextInput(TextInputProperties properties)
        {
            window.TextInput += handleTextInput;
            window.TextEditing += handleTextEditing;
            window.StartTextInput(properties);
        }

        protected override void EnsureTextInputActivated(TextInputProperties properties)
        {
            window.StartTextInput(properties);
        }

        protected override void DeactivateTextInput()
        {
            window.TextInput -= handleTextInput;
            window.TextEditing -= handleTextEditing;
            window.StopTextInput();
        }

        public override void SetImeRectangle(RectangleF rectangle)
        {
            window.SetTextInputRect(rectangle);
        }

        public override void ResetIme()
        {
            base.ResetIme();
            window.ResetIme();
        }
    }
}
