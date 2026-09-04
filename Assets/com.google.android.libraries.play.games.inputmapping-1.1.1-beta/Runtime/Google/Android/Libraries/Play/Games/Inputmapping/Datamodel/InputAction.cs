using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
{
    public class InputAction : global::Google.LibraryWrapper.Java.JavaObject
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;
        private static readonly IntPtr _cachedMethodId1;
        private static readonly IntPtr _cachedMethodId2;
        private static readonly IntPtr _cachedMethodId3;
        private static readonly IntPtr _cachedMethodId4;
        private static readonly IntPtr _cachedMethodId5;
        private static readonly IntPtr _cachedMethodId6;
        private static readonly IntPtr _cachedMethodId7;

        static InputAction()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/datamodel/InputAction");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "actionLabel", "()Ljava/lang/String;");
            _cachedMethodId1 = AndroidJNI.GetMethodID(_classObject, "inputControls", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputControls;");
            _cachedMethodId2 = AndroidJNI.GetMethodID(_classObject, "inputActionId", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;");
            _cachedMethodId3 = AndroidJNI.GetMethodID(_classObject, "inputRemappingOption", "()I");
            _cachedMethodId4 = AndroidJNI.GetMethodID(_classObject, "remappedInputControls", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputControls;");
            _cachedMethodId5 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputControls;Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputIdentifier;I)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputAction;");
            _cachedMethodId6 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;JLcom/google/android/libraries/play/games/inputmapping/datamodel/InputControls;I)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputAction;");
            _cachedMethodId7 = AndroidJNI.GetStaticMethodID(_classObject, "create", "(Ljava/lang/String;JLcom/google/android/libraries/play/games/inputmapping/datamodel/InputControls;)Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputAction;");
        }

        public InputAction(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputAction.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::System.String ActionLabel()
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

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls InputControls()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId1, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier InputActionId()
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

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls RemappedInputControls()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId4, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction Create(global::System.String actionLabel, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls inputControls, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputIdentifier inputActionId, int inputRemappingOption)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(actionLabel) },
                            new jvalue { l = inputControls.GetRawObject() },
                            new jvalue { l = inputActionId.GetRawObject() },
                            new jvalue { i = inputRemappingOption }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId5, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction Create(global::System.String actionLabel, long uniqueId, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls inputControls, int inputRemappingOption)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(actionLabel) },
                            new jvalue { j = uniqueId },
                            new jvalue { l = inputControls.GetRawObject() },
                            new jvalue { i = inputRemappingOption }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId6, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction Create(global::System.String actionLabel, long uniqueId, global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputControls inputControls)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ =
                        new jvalue[]
                        {
                            new jvalue { l = global::Google.LibraryWrapper.Java.Utils.NewString(actionLabel) },
                            new jvalue { j = uniqueId },
                            new jvalue { l = inputControls.GetRawObject() }
                        };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputAction(AndroidJNI.CallStaticObjectMethod(_classObject, _cachedMethodId7, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputAction wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputAction(IntPtr rawObject)
        {
            return new InputAction(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputAction
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping.Datamodel
