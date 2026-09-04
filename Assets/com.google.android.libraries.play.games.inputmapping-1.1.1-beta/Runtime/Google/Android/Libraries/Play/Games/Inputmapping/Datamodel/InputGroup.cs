using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class InputGroup : global::Google.LibraryWrapper.Java.JavaObject
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;
        private static readonly IntPtr _cachedMethodId1;
        private static readonly IntPtr _cachedMethodId2;
        private static readonly IntPtr _cachedMethodId3;
        private static readonly IntPtr _cachedMethodId4;
        private static readonly IntPtr _cachedMethodId5;
        private static readonly IntPtr _cachedMethodId6;

        static InputGroup()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/InputGroup");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "groupLabel", "()Ljava/lang/String;");
            _cachedMethodId1 = AndroidJNI.GetMethodID(_classObject, "inputActions", "()Ljava/util/List;");
            _cachedMethodId2 = AndroidJNI.GetMethodID(_classObject, "inputGroupId", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;");
            _cachedMethodId3 = AndroidJNI.GetMethodID(_classObject, "inputRemappingOption", "()I");
            _cachedMethodId4 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;Ljava/util/List;Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;I)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputGroup;");
            _cachedMethodId5 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;Ljava/util/List;JI)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputGroup;");
            _cachedMethodId6 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;Ljava/util/List;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputGroup;");
        }

        public InputGroup(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputGroup.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::System.String GroupLabel()
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

        public global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction> InputActions()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Java.Util.ListAnonymousImplementation<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction>(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId1, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier InputGroupId()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId2, args_));
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
                return AndroidJNI.CallIntMethod(_rawObject, _cachedMethodId3, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup Create(global::System.String groupLabel, global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction> inputActions, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier inputGroupId, int inputRemappingOption)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(groupLabel) },
                            new jvalue { l = inputActions.GetRawObject() },
                            new jvalue { l = inputGroupId.GetRawObject() },
                            new jvalue { i = inputRemappingOption }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId4, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup Create(global::System.String groupLabel, global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction> inputActions, long uniqueId, int inputRemappingOption)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(groupLabel) },
                            new jvalue { l = inputActions.GetRawObject() },
                            new jvalue { j = uniqueId },
                            new jvalue { i = inputRemappingOption }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId5, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup Create(global::System.String groupLabel, global::Java.Util.List<global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction> inputActions)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(groupLabel) },
                            new jvalue { l = inputActions.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputGroup(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId6, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputGroup wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputGroup(IntPtr rawObject)
        {
            return new InputGroup(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputGroup
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
