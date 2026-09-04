using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class InputControls : global::Google.LibraryWrapper.Java.JavaObject
    {
        public static int MOUSE_ACTION_UNSPECIFIED
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId0);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId0, value);
            }
        }
        public static int MOUSE_RIGHT_CLICK
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId1);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId1, value);
            }
        }
        public static int MOUSE_TERTIARY_CLICK
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId2);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId2, value);
            }
        }
        public static int MOUSE_FORWARD_CLICK
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId3);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId3, value);
            }
        }
        public static int MOUSE_BACK_CLICK
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId4);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId4, value);
            }
        }
        public static int MOUSE_SCROLL_UP
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId5);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId5, value);
            }
        }
        public static int MOUSE_SCROLL_DOWN
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId6);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId6, value);
            }
        }
        public static int MOUSE_MOVEMENT
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId7);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId7, value);
            }
        }
        public static int MOUSE_LEFT_DRAG
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId8);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId8, value);
            }
        }
        public static int MOUSE_RIGHT_DRAG
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId9);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId9, value);
            }
        }
        public static int MOUSE_LEFT_CLICK
        {
            get
            {
                return AndroidJNI.GetStaticIntField(_classObject, _cachedFieldId10);
            }
            set
            {
                AndroidJNI.SetStaticIntField(_classObject, _cachedFieldId10, value);
            }
        }

        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedFieldId0;
        private static readonly IntPtr _cachedFieldId1;
        private static readonly IntPtr _cachedFieldId2;
        private static readonly IntPtr _cachedFieldId3;
        private static readonly IntPtr _cachedFieldId4;
        private static readonly IntPtr _cachedFieldId5;
        private static readonly IntPtr _cachedFieldId6;
        private static readonly IntPtr _cachedFieldId7;
        private static readonly IntPtr _cachedFieldId8;
        private static readonly IntPtr _cachedFieldId9;
        private static readonly IntPtr _cachedFieldId10;
        private static readonly IntPtr _cachedMethodId0;
        private static readonly IntPtr _cachedMethodId1;
        private static readonly IntPtr _cachedMethodId2;

        static InputControls()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/InputControls");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedFieldId0 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_ACTION_UNSPECIFIED", "I");
            _cachedFieldId1 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_RIGHT_CLICK", "I");
            _cachedFieldId2 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_TERTIARY_CLICK", "I");
            _cachedFieldId3 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_FORWARD_CLICK", "I");
            _cachedFieldId4 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_BACK_CLICK", "I");
            _cachedFieldId5 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_SCROLL_UP", "I");
            _cachedFieldId6 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_SCROLL_DOWN", "I");
            _cachedFieldId7 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_MOVEMENT", "I");
            _cachedFieldId8 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_LEFT_DRAG", "I");
            _cachedFieldId9 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_RIGHT_DRAG", "I");
            _cachedFieldId10 = AndroidJNI.GetStaticFieldID(_classObject, "MOUSE_LEFT_CLICK", "I");
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "keycodes", "()Ljava/util/List;");
            _cachedMethodId1 = AndroidJNI.GetMethodID(_classObject, "mouseActions", "()Ljava/util/List;");
            _cachedMethodId2 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/util/List;Ljava/util/List;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputControls;");
        }

        public InputControls(IntPtr rawObject) : base(IntPtr.Zero)
        {
            if (rawObject == IntPtr.Zero)
            {
                return;
            }
            try
            {
                AndroidJNI.PushLocalFrame(0);
                IntPtr instanceClassObject_ = AndroidJNI.GetObjectClass(rawObject);
                if (!AndroidJNI.IsAssignableFrom(instanceClassObject_, _classObject))
                {
                    throw new ArgumentException("The provided reference is not an instance of InputControls.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Java.Util.List<global::Java.Lang.Integer> Keycodes()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Java.Util.ListAnonymousImplementation<global::Java.Lang.Integer>(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId0, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Java.Util.List<global::Java.Lang.Integer> MouseActions()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Java.Util.ListAnonymousImplementation<global::Java.Lang.Integer>(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId1, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls Create(global::Java.Util.List<global::Java.Lang.Integer> keycodes, global::Java.Util.List<global::Java.Lang.Integer> mouseActions)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = keycodes.GetRawObject() },
                            new jvalue { l = mouseActions.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId2, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputControls wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputControls(IntPtr rawObject)
        {
            return new InputControls(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputControls
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
