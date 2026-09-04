using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping
{
    public interface InputRemappingListener : global::Google.LibraryWrapper.Java.JavaInterface
    {

        void OnInputMapChanged(global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap inputMap);
    } // end class InputRemappingListener

    public class InputRemappingListenerAnonymousImplementation : global::Google.LibraryWrapper.Java.JavaObject, global::Google.Android.Libraries.Play.Games.Inputmapping.InputRemappingListener
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;

        static InputRemappingListenerAnonymousImplementation()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/InputRemappingListener");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "onInputMapChanged", "(Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputMap;)V");
        }

        public InputRemappingListenerAnonymousImplementation(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputRemappingListenerAnonymousImplementation.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public void OnInputMapChanged(global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap inputMap)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] { new jvalue { l = inputMap.GetRawObject() } };
                AndroidJNI.CallVoidMethod(_rawObject, _cachedMethodId0, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputRemappingListenerAnonymousImplementation wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputRemappingListenerAnonymousImplementation(IntPtr rawObject)
        {
            return new InputRemappingListenerAnonymousImplementation(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputRemappingListenerAnonymousImplementation

    public abstract class InputRemappingListenerCallbackHelper : global::Google.LibraryWrapper.Java.JavaProxy, global::Google.Android.Libraries.Play.Games.Inputmapping.InputRemappingListener
    {
        public InputRemappingListenerCallbackHelper() : base("com.google.android.libraries.play.games.inputmapping.InputRemappingListener") {}

        public abstract void OnInputMapChanged(global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap inputMap);

        public override sealed AndroidJavaObject Invoke(global::System.String methodName, global::System.Object[] args)
        {
            if (methodName == "onInputMapChanged" && args.Length == 1)
            {
                OnInputMapChanged(new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputMap(((AndroidJavaObject) args[0]).GetRawObject()));
                return null;
            }
            return base.Invoke(methodName, args);
        }
    } // end class InputRemappingListenerCallbackHelper
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping
