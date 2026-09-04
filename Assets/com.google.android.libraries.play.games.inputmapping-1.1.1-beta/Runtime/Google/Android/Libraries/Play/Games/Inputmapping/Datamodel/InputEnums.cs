using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class InputEnums : global::Google.LibraryWrapper.Java.JavaObject
    {
        public static int REMAP_OPTION_UNSPECIFIED
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
        public static int REMAP_OPTION_ENABLED
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
        public static int REMAP_OPTION_DISABLED
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

        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedFieldId0;
        private static readonly IntPtr _cachedFieldId1;
        private static readonly IntPtr _cachedFieldId2;

        static InputEnums()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/InputEnums");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedFieldId0 = AndroidJNI.GetStaticFieldID(_classObject, "REMAP_OPTION_UNSPECIFIED", "I");
            _cachedFieldId1 = AndroidJNI.GetStaticFieldID(_classObject, "REMAP_OPTION_ENABLED", "I");
            _cachedFieldId2 = AndroidJNI.GetStaticFieldID(_classObject, "REMAP_OPTION_DISABLED", "I");
        }

        public InputEnums(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputEnums.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputEnums wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputEnums(IntPtr rawObject)
        {
            return new InputEnums(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputEnums
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
