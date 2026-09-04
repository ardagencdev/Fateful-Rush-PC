using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class InputContext : global::Google.LibraryWrapper.Java.JavaObject
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;
        private static readonly IntPtr _cachedMethodId1;
        private static readonly IntPtr _cachedMethodId2;
        private static readonly IntPtr _cachedMethodId3;
        private static readonly IntPtr _cachedMethodId4;

        static InputContext()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/InputContext");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "localizedContextLabel", "()Ljava/lang/String;");
            _cachedMethodId1 = AndroidJNI.GetMethodID(_classObject, "inputContextId", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;");
            _cachedMethodId2 = AndroidJNI.GetMethodID(_classObject, "activeRemappingGroups", "()Ljava/util/List;");
            _cachedMethodId3 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;Ljava/util/List;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputContext;");
            _cachedMethodId4 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;JLjava/util/List;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputContext;");
        }

        public InputContext(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputContext.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::System.String LocalizedContextLabel()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return AndroidJNI.CallStringMethod(_rawObject, _cachedMethodId0, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier InputContextId()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId1, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup> ActiveRemappingGroups()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Java.Util.ListAnonymousImplementation<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup>(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId2, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext Create(global::System.String localizedContextLabel, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier inputContextId, global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup> activeRemappingGroups)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(localizedContextLabel) },
                            new jvalue { l = inputContextId.GetRawObject() },
                            new jvalue { l = activeRemappingGroups.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId3, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext Create(global::System.String localizedContextLabel, long uniqueId, global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup> activeRemappingGroups)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(localizedContextLabel) },
                            new jvalue { j = uniqueId },
                            new jvalue { l = activeRemappingGroups.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId4, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputContext wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputContext(IntPtr rawObject)
        {
            return new InputContext(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputContext
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
