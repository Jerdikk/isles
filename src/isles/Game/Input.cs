// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Isles;

public class Input
{
    private MouseState mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
    private MouseState mouseStateLastFrame;

    /// <summary>
    /// Keyboard state, set every frame in the Update method.
    /// Note: KeyboardState is a class and not a struct,
    /// we have to initialize it here, else we might run into trouble when
    /// accessing any keyboardState data before BaseGame.Update() is called.
    /// We can also NOT use the last state because everytime we call
    /// Keyboard.GetState() the old state is useless (see XNA help for more
    /// information, section Input). We store our own array of keys from
    /// the last frame for comparing stuff.
    /// </summary>
    private KeyboardState keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

    /// <summary>
    /// Keys pressed last frame, for comparison if a key was just pressed.
    /// </summary>
    private List<Keys> keysPressedLastFrame = new();
    private int mouseWheelValue;

    public Point MousePosition => new(mouseState.X, mouseState.Y);

    public MouseState Mouse => mouseState;

    public int MouseWheelDelta { get; private set; }

    public bool MouseInBox(Rectangle rect)
    {
        return mouseState.X >= rect.X &&
               mouseState.Y >= rect.Y &&
               mouseState.X < rect.Right &&
               mouseState.Y < rect.Bottom;
    }

    public KeyboardState Keyboard => keyboardState;

    public bool IsShiftPressed => keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

    public static bool IsSpecialKey(Keys key)
    {
        // All keys except A-Z, 0-9 and `-\[];',./= (and space) are special keys.
        // With shift pressed this also results in this keys:
        // ~_|{}:"<>? !@#$%^&*().
        var keyNum = (int)key;
        if ((keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z) ||
            (keyNum >= (int)Keys.D0 && keyNum <= (int)Keys.D9) ||
            key == Keys.Space || // well, space ^^
            key == Keys.OemTilde || // `~
            key == Keys.OemMinus || // -_
            key == Keys.OemPipe || // \|
            key == Keys.OemOpenBrackets || // [{
            key == Keys.OemCloseBrackets || // ]}
            key == Keys.OemQuotes || // '"
            key == Keys.OemQuestion || // /?
            key == Keys.OemPlus) // =+
        {
            return false;
        }

        // Else is is a special key
        return true;
    }

    /// <summary>
    /// Key to char helper conversion method.
    /// Note: If the keys are mapped other than on a default QWERTY
    /// keyboard, this method will not work properly. Most keyboards
    /// will return the same for A-Z and 0-9, but the special keys
    /// might be different.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <returns>Char.</returns>
    public static char KeyToChar(Keys key, bool shiftPressed)
    {
        // If key will not be found, just return space
        var ret = ' ';
        var keyNum = (int)key;
        if (keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z)
        {
            ret = shiftPressed ? key.ToString()[0] : key.ToString().ToLower()[0];
        }
        else if (keyNum >= (int)Keys.D0 && keyNum <= (int)Keys.D9 &&
            shiftPressed == false)
        {
            ret = (char)('0' + (keyNum - Keys.D0));
        }
        else if (key == Keys.D1 && shiftPressed)
        {
            ret = '!';
        }
        else if (key == Keys.D2 && shiftPressed)
        {
            ret = '@';
        }
        else if (key == Keys.D3 && shiftPressed)
        {
            ret = '#';
        }
        else if (key == Keys.D4 && shiftPressed)
        {
            ret = '$';
        }
        else if (key == Keys.D5 && shiftPressed)
        {
            ret = '%';
        }
        else if (key == Keys.D6 && shiftPressed)
        {
            ret = '^';
        }
        else if (key == Keys.D7 && shiftPressed)
        {
            ret = '&';
        }
        else if (key == Keys.D8 && shiftPressed)
        {
            ret = '*';
        }
        else if (key == Keys.D9 && shiftPressed)
        {
            ret = '(';
        }
        else if (key == Keys.D0 && shiftPressed)
        {
            ret = ')';
        }
        else if (key == Keys.OemTilde)
        {
            ret = shiftPressed ? '~' : '`';
        }
        else if (key == Keys.OemMinus)
        {
            ret = shiftPressed ? '_' : '-';
        }
        else if (key == Keys.OemPipe)
        {
            ret = shiftPressed ? '|' : '\\';
        }
        else if (key == Keys.OemOpenBrackets)
        {
            ret = shiftPressed ? '{' : '[';
        }
        else if (key == Keys.OemCloseBrackets)
        {
            ret = shiftPressed ? '}' : ']';
        }
        else if (key == Keys.OemSemicolon)
        {
            ret = shiftPressed ? ':' : ';';
        }
        else if (key == Keys.OemQuotes)
        {
            ret = shiftPressed ? '"' : '\'';
        }
        else if (key == Keys.OemComma)
        {
            ret = shiftPressed ? '<' : '.';
        }
        else if (key == Keys.OemPeriod)
        {
            ret = shiftPressed ? '>' : ',';
        }
        else if (key == Keys.OemQuestion)
        {
            ret = shiftPressed ? '?' : '/';
        }
        else if (key == Keys.OemPlus)
        {
            ret = shiftPressed ? '+' : '=';
        }

        // Return result
        return ret;
    }

    private struct Entry
    {
        public float Order;
        public IEventListener Handler;

        public Entry(IEventListener handler, float order)
        {
            Order = order;
            Handler = handler;
        }
    }

    private readonly LinkedList<Entry> handlers = new();

    public void Register(IEventListener handler, float order)
    {
        if (null == handler)
        {
            throw new ArgumentNullException();
        }

        // Check if this handler already exists in the list
        LinkedListNode<Entry> p = handlers.First;

        while (p != null)
        {
            if (p.Value.Handler == handler)
            {
                return;
            }

            p = p.Next;
        }

        // Add to the list
        p = handlers.First;

        while (p != null)
        {
            if (p.Value.Order > order)
            {
                handlers.AddBefore(p, new Entry(handler, order));
                return;
            }

            p = p.Next;
        }

        handlers.AddLast(new Entry(handler, order));
    }

    private IEventListener captured;

    /// <summary>
    /// Capture input event. All input events will always (only) be
    /// sent to the specified handler.
    /// </summary>
    public void Capture(IEventListener handler)
    {
        if (captured != null)
        {
            throw new InvalidOperationException();
        }

        captured = handler;
    }

    /// <summary>
    /// Release the capture of input events.
    /// </summary>
    public void Uncapture()
    {
        captured = null;
    }

    /// <summary>
    /// Time interval for double click, measured in seconds.
    /// </summary>
    public const float DoubleClickInterval = 0.25f;

    /// <summary>
    /// Flag for simulating double click.
    /// </summary>
    private int doubleClickFlag;
    private double doubleClickTime;

    public void Update(GameTime gameTime)
    {
        // Handle mouse input variables
        mouseStateLastFrame = mouseState;
        mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

        MouseWheelDelta = mouseState.ScrollWheelValue - mouseWheelValue;
        mouseWheelValue = mouseState.ScrollWheelValue;

        // Handle keyboard input
        keysPressedLastFrame = new List<Keys>(keyboardState.GetPressedKeys());
        keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

        // Mouse wheel event
        if (MouseWheelDelta != 0)
        {
            TriggerEvent(EventType.Wheel, null);
        }

        // Mouse Left Button Events
        if (mouseState.LeftButton == ButtonState.Pressed &&
            mouseStateLastFrame.LeftButton == ButtonState.Released)
        {
            if (doubleClickFlag == 0)
            {
                doubleClickFlag = 1;
                doubleClickTime = gameTime.TotalGameTime.TotalSeconds;
                TriggerEvent(EventType.LeftButtonDown, null);
            }
            else if (doubleClickFlag == 1)
            {
                if ((gameTime.TotalGameTime.TotalSeconds - doubleClickTime) < DoubleClickInterval)
                {
                    doubleClickFlag = 0;
                    TriggerEvent(EventType.DoubleClick, null);
                }
                else
                {
                    doubleClickTime = gameTime.TotalGameTime.TotalSeconds;
                    TriggerEvent(EventType.LeftButtonDown, null);
                }
            }
        }
        else if (mouseState.LeftButton == ButtonState.Released &&
                 mouseStateLastFrame.LeftButton == ButtonState.Pressed)
        {
            TriggerEvent(EventType.LeftButtonUp, null);
        }

        // Mouse Right Button Events
        if (mouseState.RightButton == ButtonState.Pressed &&
            mouseStateLastFrame.RightButton == ButtonState.Released)
        {
            TriggerEvent(EventType.RightButtonDown, null);
        }
        else if (mouseState.RightButton == ButtonState.Released &&
                 mouseStateLastFrame.RightButton == ButtonState.Pressed)
        {
            TriggerEvent(EventType.RightButtonUp, null);
        }

        // Mouse Middle Button Events
        if (mouseState.MiddleButton == ButtonState.Pressed &&
            mouseStateLastFrame.MiddleButton == ButtonState.Released)
        {
            TriggerEvent(EventType.MiddleButtonDown, null);
        }
        else if (mouseState.MiddleButton == ButtonState.Released &&
                 mouseStateLastFrame.MiddleButton == ButtonState.Pressed)
        {
            TriggerEvent(EventType.MiddleButtonUp, null);
        }

        // Key down events
        foreach (Keys key in keyboardState.GetPressedKeys())
        {
            if (!keysPressedLastFrame.Contains(key))
            {
                TriggerEvent(EventType.KeyDown, key);
            }
        }

        // Key up events
        foreach (Keys key in keysPressedLastFrame)
        {
            var found = false;
            foreach (Keys keyCurrent in keyboardState.GetPressedKeys())
            {
                if (keyCurrent == key)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                TriggerEvent(EventType.KeyUp, key);
            }
        }
    }

    private void TriggerEvent(EventType type, Keys? key)
    {
        // If we're captured, only send the event to
        // the hooker.
        if (captured != null)
        {
            captured.HandleEvent(type, this, key);
            return;
        }

        // Otherwise pump down through all handlers.
        // Stop when the event is handled.
        foreach (Entry entry in handlers)
        {
            if (entry.Handler.HandleEvent(type, this, key) ==
                EventResult.Handled)
            {
                break;
            }
        }
    }
}
