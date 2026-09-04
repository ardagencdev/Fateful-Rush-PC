using System;

using UnityEngine;

namespace Google.Android.Libraries.Play.Games.Inputmapping
{
    public interface InputMappingClient : global::Google.LibraryWrapper.Java.JavaInterface
    {

        void SetInputMappingProvider(global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProvider inputMappingProvider);

        void ClearInputMappingProvider();

        void SetInputContext(global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext inputContext);

        void RegisterRemappingListener(global::Google.Android.Libraries.Play.Games.Inputmapping.InputRemappingListener inputRemappingListener);

        void ClearRemappingListener();
    } // end class InputMappingClient

    public class InputMappingClientAnonymousImplementation : global::Google.LibraryWrapper.Java.JavaObject, global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingClient
    {
        private static readonly IntPtr _classObject;
        private static readonly IntPtr _cachedMethodId0;
        private static readonly IntPtr _cachedMethodId1;
        private static readonly IntPtr _cachedMethodId2;
        private static readonly IntPtr _cachedMethodId3;
        private static readonly IntPtr _cachedMethodId4;

        static InputMappingClientAnonymousImplementation()
        {
            AndroidJNI.AttachCurrentThread();
            IntPtr classObject = AndroidJNI.FindClass("com/google/android/libraries/play/games/inputmapping/InputMappingClient");
            _classObject = AndroidJNI.NewGlobalRef(classObject);
            AndroidJNI.DeleteLocalRef(classObject);
            _cachedMethodId0 = AndroidJNI.GetMethodID(_classObject, "setInputMappingProvider", "(Lcom/google/android/libraries/play/games/inputmapping/InputMappingProvider;)V");
            _cachedMethodId1 = AndroidJNI.GetMethodID(_classObject, "clearInputMappingProvider", "()V");
            _cachedMethodId2 = AndroidJNI.GetMethodID(_classObject, "setInputContext", "(Lcom/google/android/libraries/play/games/inputmapping/datamodel/InputContext;)V");
            _cachedMethodId3 = AndroidJNI.GetMethodID(_classObject, "registerRemappingListener", "(Lcom/google/android/libraries/play/games/inputmapping/InputRemappingListener;)V");
            _cachedMethodId4 = AndroidJNI.GetMethodID(_classObject, "clearRemappingListener", "()V");
        }

        public InputMappingClientAnonymousImplementation(IntPtr rawObject) : base(IntPtr.Zero)
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
                    throw new ArgumentException("The provided reference is not an instance of InputMappingClientAnonymousImplementation.");
                }
                _rawObject = AndroidJNI.NewGlobalRef(rawObject);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public void SetInputMappingProvider(global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProvider inputMappingProvider)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] { new jvalue { l = inputMappingProvider.GetRawObject() } };
                AndroidJNI.CallVoidMethod(_rawObject, _cachedMethodId0, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public void ClearInputMappingProvider()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                AndroidJNI.CallVoidMethod(_rawObject, _cachedMethodId1, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public void SetInputContext(global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext inputContext)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] { new jvalue { l = inputContext.GetRawObject() } };
                AndroidJNI.CallVoidMethod(_rawObject, _cachedMethodId2, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public void RegisterRemappingListener(global::Google.Android.Libraries.Play.Games.Inputmapping.InputRemappingListener inputRemappingListener)
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] { new jvalue { l = inputRemappingListener.GetRawObject() } };
                AndroidJNI.CallVoidMethod(_rawObject, _cachedMethodId3, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public void ClearRemappingListener()
        {
            try
            {
                AndroidJNI.PushLocalFrame(0);
                jvalue[] args_ = new jvalue[] {  };
                AndroidJNI.CallVoidMethod(_rawObject, _cachedMethodId4, args_);
            }
            finally
            {
                AndroidJNI.PopLocalFrame(IntPtr.Zero);
            }
        }

        public static explicit operator IntPtr(InputMappingClientAnonymousImplementation wrapper)
        {
            return wrapper.GetRawObject();
        }

        public static explicit operator InputMappingClientAnonymousImplementation(IntPtr rawObject)
        {
            return new InputMappingClientAnonymousImplementation(rawObject);
        }

        public new static IntPtr GetRawClass()
        {
            return AndroidJNI.NewLocalRef(_classObject);
        }
    } // end class InputMappingClientAnonymousImplementation

    public abstract class InputMappingClientCallbackHelper : global::Google.LibraryWrapper.Java.JavaProxy, global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingClient
    {
        public InputMappingClientCallbackHelper() : base("com.google.android.libraries.play.games.inputmapping.InputMappingClient") {}

        public abstract void SetInputMappingProvider(global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProvider inputMappingProvider);

        public abstract void ClearInputMappingProvider();

        public abstract void SetInputContext(global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext inputContext);

        public abstract void RegisterRemappingListener(global::Google.Android.Libraries.Play.Games.Inputmapping.InputRemappingListener inputRemappingListener);

        public abstract void ClearRemappingListener();

        public override sealed AndroidJavaObject Invoke(global::System.String methodName, global::System.Object[] args)
        {
            if (methodName == "setInputMappingProvider" && args.Length == 1)
            {
                SetInputMappingProvider(new global::Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProviderAnonymousImplementation(((AndroidJavaObject) args[0]).GetRawObject()));
                return null;
            }
            else if (methodName == "clearInputMappingProvider" && args.Length == 0)
            {
                ClearInputMappingProvider();
                return null;
            }
            else if (methodName == "setInputContext" && args.Length == 1)
            {
                SetInputContext(new global::Google.Android.Libraries.Play.Games.Inputmapping.Datamodel.InputContext(((AndroidJavaObject) args[0]).GetRawObject()));
                return null;
            }
            else if (methodName == "registerRemappingListener" && args.Length == 1)
            {
                RegisterRemappingListener(new global::Google.Android.Libraries.Play.Games.Inputmapping.InputRemappingListenerAnonymousImplementation(((AndroidJavaObject) args[0]).GetRawObject()));
                return null;
            }
            else if (methodName == "clearRemappingListener" && args.Length == 0)
            {
                ClearRemappingListener();
                return null;
            }
            return base.Invoke(methodName, args);
        }
    } // end class InputMappingClientCallbackHelper
} // end namespace Google.Android.Libraries.Play.Games.Inputmapping
