using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping
{
    public interface InputMappingProvider : global::Google.LibraryWrapper.Java.JavaInterface
    {

        global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap OnProvideInputMap();
    } // end class InputMappingProvider

    public class InputMappingProviderAnonymousImplementation : global::Google.LibraryWrapper.Java.JavaObject, global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProvider
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;

        static InputMappingProviderAnonymousImplementation()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/InputMappingProvider");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "onProvideInputMap", "()Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputMap;");
        }

        public InputMappingProviderAnonymousImplementation(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputMappingProviderAnonymousImplementation.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap OnProvideInputMap()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                return new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap(AndroidJNI.CallObjectMethod(_rawObject, _cachedMethodId0, args_));
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputMappingProviderAnonymousImplementation wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputMappingProviderAnonymousImplementation(IntPtr rawObject)
        {
            return new InputMappingProviderAnonymousImplementation(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputMappingProviderAnonymousImplementation

    public abstract class InputMappingProviderCallbackHelper : global::Google.LibraryWrapper.Java.JavaProxy, global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProvider
    {
        public InputMappingProviderCallbackHelper() : base("com.google.android.libraries.play.games.inputmapping.InputMappingProvider") {}

        public abstract global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap OnProvideInputMap();

        public override sealed AndroidJavaObject Invoke(global::System.String methodName, global::System.Object[] args)
        {
            if (methodName == "onProvideInputMap" && args.Length == 0)
            {
                return Google.LibraryWrapper.Java.Utils.ToAndroidJavaObject(OnProvideInputMap());
            }
            return base.Invoke(methodName, args);
        }
    } // end class InputMappingProviderCallbackHelper
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping
