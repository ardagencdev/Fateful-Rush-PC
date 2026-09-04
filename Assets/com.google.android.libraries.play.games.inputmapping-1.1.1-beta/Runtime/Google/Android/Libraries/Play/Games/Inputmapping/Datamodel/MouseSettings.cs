using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class MouseSettings : global::Google.LibraryWrapper.Java.JavaObject
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;

        static MouseSettings()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/MouseSettings");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(ZZ)Lcom/google/android/libraries/play/games/inputmapping/datamodel/MouseSettings;");
        }

        public MouseSettings(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of MouseSettings.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.MouseSettings Create(bool allowMouseSensitivityAdjustment, bool invertMouseMovement)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { z = allowMouseSensitivityAdjustment },
                            new jvalue { z = invertMouseMovement }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.MouseSettings(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId0, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(MouseSettings wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator MouseSettings(IntPtr rawObject)
        {
            return new MouseSettings(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class MouseSettings
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
