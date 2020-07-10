using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Core;
using Unity.Tiny;
using Unity.Tiny.Assertions;
using Unity.Transforms;

/// <summary>
///  @module
///  @name Unity.Tiny
/// </summary>
namespace Unity.Tiny.Input
{
    /// <summary>
    /// Pointer type
    /// </summary>
    public enum PointerType
    {
        None = 0,
        Mouse = 1,
        Touch = 2,
        Pen = 3
    };

    /// <summary>
    /// Pointer mode type
    /// </summary>
    public enum PointerModeType
    {
        /// <summary>
        /// Cursor behaves in default manner according to OS
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Cursor is hidden and locked to center of window. Window must get input focus for this
        /// to activate, and if input focus is lost (i.e. switch to another window) the cursor
        /// is unlocked
        /// </summary>
        Locked = 1,

        /// <summary>
        /// Cursor is hidden (so you can provide your own) but not locked to the window
        /// </summary>
        Hidden = 2
    }

    /// <summary>
    /// Pointer generalizes the mouse pointer and the touch.
    /// </summary>
    public struct Pointer
    {
        /// <summary>
        /// The unique id of the pointer. Use it with the Input.getPointerById() method.
        /// </summary>
        public int id;

        /// <summary>
        /// The type of the pointer.
        /// </summary>
        public PointerType type;

        /// <summary>
        ///  Specifies the absolute position of the pointer, in pixels on the browser
        ///  or application window. A value of 0 corresponds to the leftmost edge of
        ///  the window. The higher the value, the farther right the coordinate.
        /// </summary>
        public float2 position;

        /// <summary>
        ///  Specifies the difference, in pixels, between the pointer position in the
        ///  current frame and the previous frame. This tells you how far the
        ///  pointer point has moved in the browser or application window.
        ///  Positive values indicate upward/right movement, and negative values downward/left movement.
        /// </summary>
        public float2 delta;

        /// <summary>
        ///  Specifies that the pointer position changed between the
        ///  previous frame and this frame.
        /// </summary>
        public bool moved;

        /// <summary>
        ///  Specifies that the pointer is expired. Expired pointers
        ///  are reported in ended state for one frame, before being cleared
        ///  from the list of registered pointers.
        /// </summary>
        public bool ended;

        /// <summary>
        /// A bit mask that specifies which of the pointer buttons is down.
        /// </summary>
        public uint down;

        /// <summary>
        /// A bit mask that specifies which of the pointer buttons was pressed in the current frame.
        /// </summary>
        public uint justDown;

        /// <summary>
        /// A bit mask that specifies which of the pointer buttons was released in the current frame.
        /// </summary>
        public uint justUp;

        /// <summary>
        /// Id of the pointer for the internal use (0 for the mouse and finger id for the touches).
        /// </summary>
        public int internalId;
    };

    /// <summary>
    ///  Describes potential states for the touch point.
    /// </summary>
    public enum TouchState
    {
        /// <summary>
        ///  The initial reported state for new touch points.
        /// </summary>
        Began,

        /// <summary>
        ///  Specifies that the touch point's position changed between the
        ///  previous frame and this frame.
        /// </summary>
        Moved,

        /// <summary>
        ///  Specifies that the touch point's position did not change between
        ///  the previous frame and this frame.
        /// </summary>
        Stationary,

        /// <summary>
        ///  Specifies that the touch point is expired. Expired Touch points
        ///  are reported in Ended state for one frame, before being cleared
        ///  from the list of registered touches.
        /// </summary>
        Ended,

        /// <summary>
        ///  Specifies that the operating system or an event interrupted the
        ///  touch interaction.
        ///
        ///  For example if an incoming call causes the the operating system
        ///  to deliver a a popup message, the application or tab switches
        ///  from one context to another.
        /// </summary>
        Canceled
    }

    /// <summary>
    ///  Stores the state for a single touch point.
    /// </summary>
    public struct Touch
    {
        /// <summary>
        ///  Specifies the difference, in pixels, between the touch point's X coordinate
        ///  in the current frame and the previous frame. This tells you how far the
        ///  touch point has moved horizontally in the browser or application window.
        ///  Positive values indicate rightward movement, and negative values leftward movement.
        /// </summary>
        public int deltaX;

        /// <summary>
        ///  Specifies the difference, in pixels, between the touch point's Y coordinate
        ///  in the current frame and the previous frame. This tells you how far the
        ///  touch point has moved vertically in the browser or application window.
        ///  Positive values indicate upward movement, and negative values downward movement.
        /// </summary>
        public int deltaY;

        /// <summary>
        ///  A unique identifier for the finger used in a touch interaction.
        /// </summary>
        public int fingerId;

        /// <summary> Specifies the life cycle state of this touch. The TouchState
        ///  enum defines the possible values
        /// </summary>
        public TouchState phase;

        /// <summary>
        ///  Specifies the absolute X coordinate of the touch, in pixels on the browser
        ///  or application window. A value of 0 corresponds to the leftmost edge of
        ///  the window. The higher the value, the farther right the coordinate.
        /// </summary>
        public int x;

        /// <summary>
        ///  Specifies the absolute Y coordinate of the touch, in pixels on the browser
        ///  or application window. A value of 0 corresponds to the bottommost edge of
        ///  the window. The higher the value, the farther up the coordinate.
        /// </summary>
        public int y;
    };

    // input service - now a system.
    // input providing systems should inherit from it
    // and overwrite the m_inputState in their system update
    [DisableAutoCreation]
    public class InputSystem : SystemBase
    {
        /// <summary>
        /// If this setting is enabled, rotation values reported by sensors are rotated around the Z axis as follows:
        /// for devices with natural Portrait orientation
        /// ScreenOrientation.Portrait - values remain unchanged.
        /// ScreenOrientation.PortraitUpsideDown - values rotate by 180 degrees.
        /// ScreenOrientation.LandscapeLeft - values rotate by 90 degrees.
        /// ScreenOrientation.LandscapeRight - values rotate by 270 degrees.
        /// for devices with natural Landscape orientation
        /// ScreenOrientation.LandscapeRight - values remain unchanged.
        /// ScreenOrientation.Portrait - values rotate by 270 degrees.
        /// ScreenOrientation.PortraitUpsideDown - values rotate by 90 degrees.
        /// ScreenOrientation.LandscapeLeft - values rotate by 180 degrees.
        /// This setting is enabled by default.
        /// </summary>
        public bool CompensateForScreenOrientation { get; set; } = true; // TODO set this from project settings

        /// <summary>
        ///  Returns true if the key is currently held down.
        /// </summary>
        public bool GetKey(KeyCode key)
        {
            return m_inputState.ContainsKey(ref m_inputState.keysPressed, key);
        }

        /// <summary>
        ///  Returns true if the key was pressed in the current frame.
        /// </summary>
        public bool GetKeyDown(KeyCode key)
        {
            return m_inputState.ContainsKey(ref m_inputState.keysJustDown, key);
        }

        /// <summary>
        ///  Returns true if the key was released in the current frame.
        /// </summary>
        public bool GetKeyUp(KeyCode key)
        {
            return m_inputState.ContainsKey(ref m_inputState.keysJustUp, key);
        }

        /// <summary>
        ///  Returns true if the mouse button is currently held down.
        /// </summary>
        public bool GetMouseButton(int button)
        {
            return (m_inputState.mousePressed & (1 << button)) != 0;
        }

        /// <summary>
        ///  Returns true if the mouse button was pressed in the current frame.
        /// </summary>
        public bool GetMouseButtonDown(int button)
        {
            return (m_inputState.mouseJustDown & (1 << button)) != 0;
        }

        /// <summary>
        ///  Returns true if the mouse button was released in the current frame.
        /// </summary>
        public bool GetMouseButtonUp(int button)
        {
            return (m_inputState.mouseJustUp & (1 << button)) != 0;
        }

        /// <summary>
        ///  Returns true if the current device produces mouse input.
        /// </summary>
        public bool IsMousePresent()
        {
            return m_inputState.hasMouse;
        }

        /// <summary>
        ///  Returns true if mouse events are emulated by other input devices, such as touch.
        /// </summary>
        public bool IsMouseEmulated()
        {
            return !m_inputState.hasMouse;
        }

        /// <summary>
        ///  Sets mouse pointer mode to normal, hidden, or locked if mouse is available on the platform.
        /// </summary>
        public virtual void SetMouseMode(PointerModeType type)
        {
        }

        /// <summary>
        ///  Returns the current mouse pointer type.
        /// </summary>
        public virtual PointerModeType GetMouseMode()
        {
            return PointerModeType.Normal;
        }

        /// <summary>
        ///  Returns true if the current device produces touch input responses.
        ///  This value may not be accurate until a first touch occurs.
        /// </summary>
        public bool IsTouchSupported()
        {
            return m_inputState.hasTouch;
        }

        /// <summary>
        ///  Returns the number of currently active touches.
        /// </summary>
        public int TouchCount()
        {
            return m_inputState.touches.Length;
        }

        /// <summary>
        ///  Retrieves information for a specific touch point. The index ranges
        ///  from 0 to the value returned by TouchCount.
        /// </summary>
        public Touch GetTouch(int index)
        {
            Assert.IsTrue(index >= 0 && index < TouchCount());
            return m_inputState.touches[index];
        }

        /// <summary>
        ///  Returns the input position in screen pixels. For touch input this is
        ///  the first touch. For mouse input, it is the mouse position.
        /// </summary>
        public float2 GetInputPosition()
        {
            return new float2((float)m_inputState.mouseX, (float)m_inputState.mouseY);
        }

        /// <summary>
        ///  Returns the input delta in screen pixels for this frame. For touch input this is
        ///  the first touch. For mouse input, it is the mouse position delta.
        /// </summary>
        public float2 GetInputDelta()
        {
            return new float2((float)m_inputState.mouseDeltaX, (float)m_inputState.mouseDeltaY);
        }

        /// <summary>
        /// Returns the current input scroll delta in screen pixels for this frame.
        /// </summary>
        public float2 GetInputScrollDelta()
        {
            return new float2(m_inputState.scrollDeltaX, m_inputState.scrollDeltaY);
        }

        /// <summary>
        ///  Convenience function that returns the value from GetInputPosition transformed
        ///  into world space. World space includes the camera transform of the camera
        ///  closest to the input position.
        ///  This is the same as TranslateScreenToWorld(GetInputPosition());
        ///  In 2D setups, the world z coordinate is always set to 0.
        /// </summary>
        public float3 GetWorldInputPosition()
        {
            // TODO: this should not live in input, but keep it for now for compat
            return TranslateScreenToWorld(GetInputPosition());
        }

        /// <summary>
        ///  Transforms Screen coordinates into World coordinates.
        ///  World space includes the camera transform of the camera closest to
        ///  the input coordinate.
        ///  In 2D setups, the world z coordinate is always set to 0.
        /// </summary>
        public float3 TranslateScreenToWorld(float2 screenCoord)
        {
            //throw new NotImplementedException();
            return new float3(0, 0, 0);
#if false
            // TODO: this should not live in input, but keep it for now for compat
            var env = World.TinyEnvironment();
            DisplayInfo di = env.GetConfigData<DisplayInfo>();

            Rect screenRect = new Rect(0, 0, (float)di.width, (float)di.height);
            float3 result = float3.zero;
            float2 windowSize = new float2((float)di.width, (float)di.height);
            float bestdepth = Single.NegativeInfinity;
            Entities.ForEach((Entity e, ref Camera2D c2d, ref LocalToWorld xform) =>
            {
                Rect cRect;
                if (c2d.rect.IsEmpty())
                    cRect = new Rect(0, 0, 1, 1);
                else
                    cRect = c2d.rect;
                cRect = screenRect.Region(cRect);
                if (cRect.Contains(screenCoord))
                {
                    if (c2d.depth > bestdepth)
                    {
                        result = TransformHelpers.WindowToWorld(this, e, screenCoord, windowSize);
                        bestdepth = c2d.depth;
                    }
                }
            });
            return result;
#endif
        }

        protected override void OnUpdate()
        {
            m_inputState.AdvanceFrame();
            Sensor.ProcessActiveSensors();
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_inputState = new InputData();
        }

        protected override void OnDestroy()
        {
            m_inputState.Dispose();
            for (int i = 0; i < m_Sensors.Count; ++i)
            {
                m_Sensors[i].Dispose();
            }
        }

        private int m_firstTouch = -1;
        private bool m_mouseInitDelta = true;
        protected void MouseEmulateReset()
        {
            m_firstTouch = -1;
            m_mouseInitDelta = true;
        }

        protected void ProcessTouch(int id, TouchState state, int x, int y)
        {
            m_inputState.TouchEvent(id, state, x, y);
            // simulate mouse from touch as well
            if (!m_inputState.hasMouse)
            {
                if (state == TouchState.Began)
                {
                    if (m_firstTouch == -1)
                    {
                        m_firstTouch = id;
                        m_inputState.MouseDown(0);
                    }
                }
                else if (state == TouchState.Ended || state == TouchState.Canceled)
                {
                    if (m_firstTouch == id)
                    {
                        m_inputState.MouseUp(0);
                        m_firstTouch = -1;
                        m_mouseInitDelta = true;
                    }
                }

                if (m_firstTouch == id)
                {
                    if (m_mouseInitDelta)
                    {
                        m_inputState.mouseDeltaX = 0;
                        m_inputState.mouseDeltaY = 0;
                        m_mouseInitDelta = false;
                    }
                    else
                    {
                        m_inputState.mouseDeltaX += x - m_inputState.mouseX;
                        m_inputState.mouseDeltaY += y - m_inputState.mouseY;
                    }
                    m_inputState.mouseX = x;
                    m_inputState.mouseY = y;
                }
            }
        }

        // update this structure in implementations
        protected InputData m_inputState;

        private List<Sensor> m_Sensors = new List<Sensor>();

        private Sensor FindSensor(System.Type type)
        {
            for (int i = 0; i < m_Sensors.Count; ++i)
            {
                var sensor = m_Sensors[i];
                if (sensor.ComponentType == type)
                    return sensor;
            }
            return null;
        }

        public void EnableSensor<T>() where T : struct, IComponentData
        {
            var sensor = FindSensor(typeof(T));
            if (sensor == null)
            {
                sensor = CreateSensor(typeof(T));
                if (sensor == null) // sensor cannot be created
                {
                    return;
                }
                m_Sensors.Add(sensor);
            }
            sensor.Enabled = true;
        }

        public void DisableSensor<T>() where T : struct, IComponentData
        {
            var sensor = FindSensor(typeof(T));
            if (sensor != null)
            {
                sensor.Enabled = false;
            }
        }

        public void SetSensorSamplingFrequency<T>(int freq) where T : IComponentData
        {
            var sensor = FindSensor(typeof(T));
            if (sensor != null)
            {
                sensor.SamplingFrequency = freq;
            }
        }

        public int GetSensorSamplingFrequency<T>() where T : struct, IComponentData
        {
            return FindSensor(typeof(T))?.SamplingFrequency ?? 0;
        }

        public bool IsAvailable<T>() where T : struct, IComponentData
        {
            return IsAvailable(typeof(T));
        }

        protected virtual Sensor CreateSensor(ComponentType type)
        {
            return null;
        }

        protected virtual bool IsAvailable(ComponentType type)
        {
            return false;
        }

        internal int GetRotationIndex()
        {
            var windowSystem = World.GetExistingSystem<WindowSystem>();
            return GetRotationIndex(windowSystem.GetOrientation());
        }

        protected virtual int GetRotationIndex(ScreenOrientation orientation)
        {
            switch (orientation)
            {
                case ScreenOrientation.Portrait: return 0;
                case ScreenOrientation.Landscape: return 1;
                case ScreenOrientation.ReversePortrait: return 2;
                case ScreenOrientation.ReverseLandscape: return 3;
            }
            // unknown
            return 0;
        }
    }

    public class InputData : IDisposable
    {
        // Sets of keyboard press states (pressed down this frame, being held down for longer time, and released up this
        // frame). Conceptually the usage is like std::unordered_sets, but we don't pull that in to our code so reuse
        // std::vector to keep code size small (STL is big)
        public NativeList<KeyCode> keysPressed = new NativeList<KeyCode>(Allocator.Persistent);
        public NativeList<KeyCode> keysJustDown = new NativeList<KeyCode>(Allocator.Persistent);
        public NativeList<KeyCode> keysJustUp = new NativeList<KeyCode>(Allocator.Persistent);

        public void Dispose()
        {
            keysPressed.Dispose();
            keysJustDown.Dispose();
            keysJustUp.Dispose();
            touches.Dispose();
        }

        public int mousePressed; // bitset
        public int mouseJustDown;
        public int mouseJustUp;

        public bool hasMouse;
        public int mouseX;
        public int mouseY;
        public int mouseDeltaX;
        public int mouseDeltaY;
        public float scrollDeltaX;
        public float scrollDeltaY;

        public bool hasTouch;
        public NativeList<Touch> touches = new NativeList<Touch>(Allocator.Persistent);

        public void AdvanceFrame()
        {
            keysJustDown.Clear();
            keysJustUp.Clear();
            mouseJustDown = 0;
            mouseJustUp = 0;

            // Advance touch lifetime for the new frame: Began events become Stationary, Ended & Canceled events are retired
            // out.
            for (int i = 0; i < touches.Length; i++)
            {
                var tt = touches[i];
                if (tt.phase == TouchState.Ended || tt.phase == TouchState.Canceled)
                {
                    touches.RemoveAtSwapBack(i);
                    i--;
                    continue;
                }
                tt.deltaX = 0;
                tt.deltaY = 0;
                if (tt.phase == TouchState.Began || tt.phase == TouchState.Moved)
                    tt.phase = TouchState.Stationary;
                touches[i] = tt;
            }
        }

        public void Clear()
        {
            keysPressed.Clear();
            keysJustDown.Clear();
            keysJustUp.Clear();
            mousePressed = 0;
            mouseJustDown = 0;
            mouseJustUp = 0;
            touches.Clear();
        }

        public void KeyDown(KeyCode key)
        {
            if (ContainsKey(ref keysPressed, key))
                return;
            keysPressed.Add(key);
            keysJustDown.Add(key);
        }

        public void KeyUp(KeyCode key)
        {
            if (!ContainsKey(ref keysPressed, key))
                return;
            RemoveKey(ref keysPressed, key);
            keysJustUp.Add(key);
        }

        public void MouseDown(int btn)
        {
            if ((mousePressed & (1 << btn)) != 0)
                return;
            mousePressed |= (1 << btn);
            mouseJustDown |= 1 << btn;
        }

        public void MouseUp(int btn)
        {
            if ((mousePressed & (1 << btn)) == 0)
                return;
            mousePressed &= ~(1 << btn);
            mouseJustUp |= 1 << btn;
        }

        public void TouchEvent(int fingerId, TouchState phase, int x, int y)
        {
            hasTouch = true; // On first touch, permanently register us to have touch support available.

            int t = FindActiveTouchById(fingerId);
            if (phase == TouchState.Began || (phase == TouchState.Moved && t == -1))
            {
                Touch newTouch;
                newTouch.deltaX = 0;
                newTouch.deltaY = 0;
                newTouch.x = x;
                newTouch.y = y;
                newTouch.phase = TouchState.Began;
                newTouch.fingerId = fingerId;
                touches.Add(newTouch);
                return;
            }
            if (t == -1 || touches[t].phase == TouchState.Ended || touches[t].phase == TouchState.Canceled)
                return;

            var tt = touches[t];
            // Accumulate touch movement delta in case multiple touch move events come within a single frame, .reset() will
            // revert the deltas to zero for the next frame.

            if (tt.phase == TouchState.Began && phase == TouchState.Moved)
              return;
            // If the Moved event is received during the same frame as the Began event, then it will mask the Began phase if not discarded

            tt.deltaX = x - tt.x;
            tt.deltaY = y - tt.y;
            tt.phase = phase;
            tt.x = x;
            tt.y = y;
            touches[t] = tt;
        }

        private int FindActiveTouchById(int fingerId)
        {
            for (int i = 0; i < touches.Length; i++)
            {
                if (touches[i].fingerId == fingerId && touches[i].phase != TouchState.Ended && touches[i].phase != TouchState.Canceled)
                    return i;
            }
            return -1;
        }

        public bool ContainsKey(ref NativeList<KeyCode> list, KeyCode c)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == c)
                    return true;
            }
            return false;
        }

        public void RemoveKey(ref NativeList<KeyCode> list, KeyCode c)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == c)
                {
                    list.RemoveAtSwapBack(i);
                    i--;
                }
            }
        }
    }

    /// <summary>
    ///  Lists key codes that you can pass to methods such as GetKey, GetKeyDown, and GetKeyUp.
    /// </summary>
    public enum KeyCode : int
    {
        // Not assigned (never returned as the result of a keystroke)
        None = 0,
        // Backspace key
        Backspace = 8,
        // Forward delete key
        Delete = 127,
        // Tab key
        Tab = 9,
        // Clear key
        Clear = 12,
        // Return key
        Return = 13,
        // Pause on PC machines
        Pause = 19,
        // Escape key
        Escape = 27,
        // Space key
        Space = 32,

        // Numeric keypad 0
        Keypad0 = 256,
        // Numeric keypad 1
        Keypad1 = 257,
        // Numeric keypad 2
        Keypad2 = 258,
        // Numeric keypad 3
        Keypad3 = 259,
        // Numeric keypad 4
        Keypad4 = 260,
        // Numeric keypad 5
        Keypad5 = 261,
        // Numeric keypad 6
        Keypad6 = 262,
        // Numeric keypad 7
        Keypad7 = 263,
        // Numeric keypad 8
        Keypad8 = 264,
        // Numeric keypad 9
        Keypad9 = 265,
        // Numeric keypad '.'
        KeypadPeriod = 266,
        // Numeric keypad '/'
        KeypadDivide = 267,
        // Numeric keypad '*'
        KeypadMultiply = 268,
        // Numeric keypad '-'
        KeypadMinus = 269,
        // Numeric keypad '+'
        KeypadPlus = 270,
        // Numeric keypad enter
        KeypadEnter = 271,
        // Numeric keypad '='
        KeypadEquals = 272,

        // Up arrow key
        UpArrow = 273,
        // Down arrow key
        DownArrow = 274,
        // Right arrow key
        RightArrow = 275,
        // Left arrow key
        LeftArrow = 276,
        // Insert key key
        Insert = 277,
        // Home key
        Home = 278,
        // End key
        End = 279,
        // Page up
        PageUp = 280,
        // Page down
        PageDown = 281,

        // F1 function key
        F1 = 282,
        // F2 function key
        F2 = 283,
        // F3 function key
        F3 = 284,
        // F4 function key
        F4 = 285,
        // F5 function key
        F5 = 286,
        // F6 function key
        F6 = 287,
        // F7 function key
        F7 = 288,
        // F8 function key
        F8 = 289,
        // F9 function key
        F9 = 290,
        // F10 function key
        F10 = 291,
        // F11 function key
        F11 = 292,
        // F12 function key
        F12 = 293,
        // F13 function key
        F13 = 294,
        // F14 function key
        F14 = 295,
        // F15 function key
        F15 = 296,

        // '0' key on the alphanumeric keyboard.
        Alpha0 = 48,
        // '1' key on the alphanumeric keyboard.
        Alpha1 = 49,
        // '2' key on the alphanumeric keyboard.
        Alpha2 = 50,
        // '3' key on the alphanumeric keyboard.
        Alpha3 = 51,
        // '4' key on the alphanumeric keyboard.
        Alpha4 = 52,
        // '5' key on the alphanumeric keyboard.
        Alpha5 = 53,
        // '6' key on the alphanumeric keyboard.
        Alpha6 = 54,
        // '7' key on the alphanumeric keyboard.
        Alpha7 = 55,
        // '8' key on the alphanumeric keyboard.
        Alpha8 = 56,
        // '9' key on the alphanumeric keyboard.
        Alpha9 = 57,

        // Exclamation mark key '!'
        Exclaim = 33,
        // Double quote key '"'
        DoubleQuote = 34,
        // Hash key '#'
        Hash = 35,
        // Dollar sign key '$'
        Dollar = 36,
        // Ampersand key '&'
        Ampersand = 38,
        // Quote key '
        Quote = 39,
        // Left Parenthesis key '('
        LeftParen = 40,
        // Right Parenthesis key ')'
        RightParen = 41,
        // Asterisk key '*'
        Asterisk = 42,
        // Plus key '+'
        Plus = 43,
        // Comma ',' key
        Comma = 44,

        // Minus '-' key
        Minus = 45,
        // Period '.' key
        Period = 46,
        // Slash '/' key
        Slash = 47,

        // Colon ':' key
        Colon = 58,
        // Semicolon ';' key
        Semicolon = 59,
        // Less than '<' key
        Less = 60,
        // Equals '=' key
        Equals = 61,
        // Greater than '>' key
        Greater = 62,
        // Question mark '?' key
        Question = 63,
        // At key '@'
        At = 64,

        // Left square bracket key '['
        LeftBracket = 91,
        // Backslash key '\'
        Backslash = 92,
        // Right square bracket key ']'
        RightBracket = 93,
        // Caret key '^'
        Caret = 94,
        // Underscore '_' key
        Underscore = 95,
        // Back quote key '`'
        BackQuote = 96,

        // 'a' key
        A = 97,
        // 'b' key
        B = 98,
        // 'c' key
        C = 99,
        // 'd' key
        D = 100,
        // 'e' key
        E = 101,
        // 'f' key
        F = 102,
        // 'g' key
        G = 103,
        // 'h' key
        H = 104,
        // 'i' key
        I = 105,
        // 'j' key
        J = 106,
        // 'k' key
        K = 107,
        // 'l' key
        L = 108,
        // 'm' key
        M = 109,
        // 'n' key
        N = 110,
        // 'o' key
        O = 111,
        // 'p' key
        P = 112,
        // 'q' key
        Q = 113,
        // 'r' key
        R = 114,
        // 's' key
        S = 115,
        // 't' key
        T = 116,
        // 'u' key
        U = 117,
        // 'v' key
        V = 118,
        // 'w' key
        W = 119,
        // 'x' key
        X = 120,
        // 'y' key
        Y = 121,
        // 'z' key
        Z = 122,

        // Numlock key
        Numlock = 300,
        // Capslock key
        CapsLock = 301,
        // Scroll lock key
        ScrollLock = 302,
        // Right shift key
        RightShift = 303,
        // Left shift key
        LeftShift = 304,
        // Right Control key
        RightControl = 305,
        // Left Control key
        LeftControl = 306,
        // Right Alt key
        RightAlt = 307,
        // Left Alt key
        LeftAlt = 308,

        // Left Command key
        LeftCommand = 310,
        // Left Command key
        LeftApple = 310,
        // Left Windows key
        LeftWindows = 311,
        // Right Command key
        RightCommand = 309,
        // Right Command key
        RightApple = 309,
        // Right Windows key
        RightWindows = 312,
        // Alt Gr key
        AltGr = 313,

        // Help key
        Help = 315,
        // Print key
        Print = 316,
        // Sys Req key
        SysReq = 317,
        // Break key
        Break = 318,
        // Menu key
        Menu = 319,

        // First (primary) mouse button
        Mouse0 = 323,
        // Second (secondary) mouse button
        Mouse1 = 324,
        // Third mouse button
        Mouse2 = 325,
        // Fourth mouse button
        Mouse3 = 326,
        // Fifth mouse button
        Mouse4 = 327,
        // Sixth mouse button
        Mouse5 = 328,
        // Seventh mouse button
        Mouse6 = 329,
    }

    public interface IPlatformSensor : IDisposable
    {
        bool Enabled { get; set; }
        int SamplingFrequency { get; set; }
        void ProcessSensorData();
    }

    public class Sensor
    {
        internal Sensor(ComponentType componentType, IPlatformSensor platformSensor)
        {
            m_EnableCounter = 0;
            ComponentType = componentType;
            m_PlatformSensor = platformSensor;
        }

        public void Dispose()
        {
            m_EnabledSensors.Remove(this);
            m_PlatformSensor?.Dispose();
        }

        public ComponentType ComponentType { get; private set; }

        public bool Enabled
        {
            get { return m_PlatformSensor?.Enabled ?? false; }
            set
            {
                if (m_PlatformSensor != null)
                {
                    if (value)
                    {
                        if (m_EnableCounter == 0)
                        {
                            m_PlatformSensor.Enabled = true;
                            if (m_PlatformSensor.Enabled)
                            {
                                ++m_EnableCounter;
                                m_EnabledSensors.Add(this);
                            }
                        }
                        else
                        {
                            ++m_EnableCounter;
                        }
                    }
                    else if (m_EnableCounter > 0)
                    {
                        if ((--m_EnableCounter) == 0)
                        {
                            m_PlatformSensor.Enabled = false;
                            m_EnabledSensors.Remove(this);
                        }
                    }
                }
            }
        }

        public int SamplingFrequency
        {
            get { return m_PlatformSensor?.SamplingFrequency ?? 0; }
            set { if (m_PlatformSensor != null) m_PlatformSensor.SamplingFrequency = value; }
        }

        public void ProcessSensorData()
        {
            m_PlatformSensor?.ProcessSensorData();
        }

        internal static void ProcessActiveSensors()
        {
            foreach (var sensor in m_EnabledSensors) sensor.ProcessSensorData();
        }

        private static List<Sensor> m_EnabledSensors = new List<Sensor>();

        private IPlatformSensor m_PlatformSensor = null;

        private int m_EnableCounter;
    }

    /// <summary>
    ///  Use the accelerometer to measure the acceleration of a device.
    ///  It reports the acceleration measured on a device both due to moving
    ///  the device around, and due to gravity pulling the device down.
    ///  Use Acceleration property (float3) to get acceleration values.
    ///  Values are affected by the InputSystem.CompensateForScreenOrientation setting.
    /// </summary>
    public struct AccelerometerSensor : IComponentData
    {
        public TimeData LastUpdateTime;
        public float3 Acceleration;
    }

    /// <summary>
    ///  Use the gyroscope to measure the angular velocity of a device.
    ///  Use AngularVelocity property (float3) to get angular velocity values.
    ///  Values are affected by the InputSystem.CompensateForScreenOrientation setting.
    /// </summary>
    public struct GyroscopeSensor : IComponentData
    {
        public TimeData LastUpdateTime;
        public float3 AngularVelocity;
    }

    /// <summary>
    ///  Use the gravity sensor to determine the direction of the gravity vector relative to a device.
    ///  Use Gravity property (float3) to get gravity vector values.
    ///  Values are affected by the InputSystem.CompensateForScreenOrientation setting.
    /// </summary>
    public struct GravitySensor : IComponentData
    {
        public TimeData LastUpdateTime;
        public float3 Gravity;
    }

    /// <summary>
    ///  Use the attitude sensor to determine the orientation of a device.
    ///  Use Attitude property (quaternion) to get device orientation values.
    ///  Values are affected by the InputSystem.CompensateForScreenOrientation setting.
    /// </summary>
    public struct AttitudeSensor : IComponentData
    {
        public TimeData LastUpdateTime;
        public quaternion Attitude;
    }

    /// <summary>
    ///  Use the accelerometer to measure the acceleration of a device.
    ///  It reports the acceleration measured on a device due to moving
    ///  the device around, without gravity effect.
    ///  Use Acceleration property (float3) to get acceleration values.
    ///  Values are affected by the InputSystem.CompensateForScreenOrientation setting.
    /// </summary>
    public struct LinearAccelerationSensor : IComponentData
    {
        public TimeData LastUpdateTime;
        public float3 Acceleration;
    }

    // Processors to convert raw sensors data to platform independent values
    internal abstract class InputProcessor<TValue> where TValue : struct
    {
        public abstract void Process(InputSystem inputSystem, ref TValue value);
    }

    internal class CompensateDirectionProcessor : InputProcessor<float3>
    {
        public override void Process(InputSystem inputSystem, ref float3 value)
        {
            if (inputSystem.CompensateForScreenOrientation)
            {
                var sin = 0.0f; var cos = 1.0f; // 0
                switch (inputSystem.GetRotationIndex())
                {
                    case 1: sin =  1.0f; cos =  0.0f; break; //  90
                    case 2: sin =  0.0f; cos = -1.0f; break; // 180
                    case 3: sin = -1.0f; cos =  0.0f; break; // 270
                }
                var x = value.x * cos - value.y * sin;
                var y = value.x * sin + value.y * cos;
                value.x = x; value.y = y;
            }
        }
    }

    internal class CompensateRotationProcessor : InputProcessor<quaternion>
    {
        public override void Process(InputSystem inputSystem, ref quaternion value)
        {
            if (inputSystem.CompensateForScreenOrientation)
            {
                var rotationIndex = inputSystem.GetRotationIndex();
                const float kSqrtOfTwo = 1.4142135623731f;
                var q = quaternion.identity;
                switch (inputSystem.GetRotationIndex())
                {
                    case 1: q = new quaternion(0.0f, 0.0f, kSqrtOfTwo * 0.5f /*sin(pi/4)*/, -kSqrtOfTwo * 0.5f /*cos(pi/4)*/); break;
                    case 2: q = new quaternion(0.0f, 0.0f, 1.0f /*sin(pi/2)*/, 0.0f /*cos(pi/2)*/); break;
                    case 3: q = new quaternion(0.0f, 0.0f, -kSqrtOfTwo * 0.5f /*sin(3pi/4)*/, -kSqrtOfTwo * 0.5f /*cos(3pi/4)*/); break;
                }
                value = math.mul(value, q);
            }
        }
    }
}
