using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class InputMap : global::Google.LibraryWrapper.Java.JavaObject
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;
        private static readonly IntPtr _cachedMethodId1;
        private static readonly IntPtr _cachedMethodId2;
        private static readonly IntPtr _cachedMethodId3;
        private static readonly IntPtr _cachedMethodId4;
        private static readonly IntPtr _cachedMethodId5;
        private static readonly IntPtr _cachedMethodId6;

        static InputMap()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/InputMap");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "inputGroups", "()Ljava/util/List;");
            _cachedMethodId1 = AndroidJNI.GetMethodID(_classObject, "mouseSettings", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/MouseSettings;");
            _cachedMethodId2 = AndroidJNI.GetMethodID(_classObject, "inputRemappingOption", "()I");
            _cachedMethodId3 = AndroidJNI.GetMethodID(_classObject, "reservedControls", "()Ljava/util/List;");
            _cachedMethodId4 = AndroidJNI.GetMethodID(_classObject, "inputMapId", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;");
            _cachedMethodId5 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/util/List;Lcom/google/android/libraries/play/games/inputmapping/datamodel/MouseSettings;Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;ILjava/util/List;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputMap;");
            _cachedMethodId6 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/util/List;Lcom/google/android/libraries/play/games/inputmapping/datamodel/MouseSettings;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputMap;");
        }

        public InputMap(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputMap.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup> InputGroups()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Java.Util.ListAnonymousImplementation<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup>(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId0, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.MouseSettings MouseSettings()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.MouseSettings(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId1, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public int InputRemappingOption()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return AndroidJNI.CallIntMethod(_rawObject, _cachedMethodId2, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls> ReservedControls()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Java.Util.ListAnonymousImplementation<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls>(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId3, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier InputMapId()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId4, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap Create(global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup> inputGroups, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.MouseSettings mouseSettings, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier inputMapId, int inputRemappingOption, global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls> reservedControls)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = inputGroups.GetRawObject() },
                            new jvalue { l = mouseSettings.GetRawObject() },
                            new jvalue { l = inputMapId.GetRawObject() },
                            new jvalue { i = inputRemappingOption },
                            new jvalue { l = reservedControls.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId5, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap Create(global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup> inputGroups, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.MouseSettings mouseSettings)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = inputGroups.GetRawObject() },
                            new jvalue { l = mouseSettings.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId6, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputMap wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputMap(IntPtr rawObject)
        {
            return new InputMap(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputMap
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
