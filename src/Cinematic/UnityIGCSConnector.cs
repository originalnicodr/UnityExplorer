﻿using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityExplorer;

namespace CinematicUnityExplorer.Cinematic
{
    // StepCommand is basically the offset of step_left and step_up, what IGCS sends to move the camera.
    using StepCommand = Mono.CSharp.Tuple<float, float>;

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dll);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
    public class UnityIGCSConnector
    {
        // UnityIGCSConnector.dll definitions.
        private delegate void MoveCameraCallback(float step_left, float step_up, float fov, int from_start);
        private delegate void SessionCallback();
        private delegate IntPtr U_IGCS_Initialize(MoveCameraCallback callback, SessionCallback start_cb, SessionCallback end_cb);

        // Store the initial position when a session start in IGCSDof.
        Mono.CSharp.Tuple<Vector3, Quaternion> position = null;

        private readonly bool isValid = false;
        private bool _isActive = false;
        public bool IsActive => isValid && _isActive;

        // Delegate holder for the MoveCamera, StartSession and EndSession.
        private readonly List<System.Delegate> delegates = new();

        // Since some games use multi-threaded, in order to make sure we're only moving things during
        // the main thread is executing, we use this Queue to enqueue the move commands and dequeue them in the Update function.
        // This object *must* be used with a Lock.
        private readonly Queue<StepCommand> commands = new();

        private IntPtr CameraStatus = IntPtr.Zero;
        // In order to avoid allocations on every Update call, we create this buffer to allocate once
        // and copy from here the CameraStatus (because Marshal.Copy requires a buffer, urgh).
        private readonly byte[] CameraStatusBuffer = new byte[] { 0x0 };

        public void UpdateFreecamStatus(bool enabled)
        {
            if (CameraStatus == IntPtr.Zero) return;

            CameraStatusBuffer[0] = enabled ? (byte)0x1 : (byte)0x0;
            Marshal.Copy(CameraStatusBuffer, 0, CameraStatus, 1);
        }

        public void ExecuteCameraCommand(Camera cam)
        {
            var transform = cam.transform;
            if (!_isActive || position == null)
            {
                position = new(transform.position, transform.rotation);
            }

            if (!_isActive || position == null) { return; }

            StepCommand c = null;

            lock (commands)
            {
                if (commands.Count <= 0) return;
                c = commands.Dequeue();
            }

            transform.position = position.Item1;
            transform.rotation = position.Item2;
            transform.Translate(c.Item1, c.Item2, 0.0f);
        }

        private void MoveCamera(float stepLeft, float stepUp, float fov, int fromStart)
        {
            lock (commands)
            {
                commands.Enqueue(new StepCommand(stepLeft, stepUp));
            }
        }
        private void StartSession()
        {
            _isActive = true;
        }

        private void EndSession()
        {
            position = null;
            _isActive = false;
        }

        public UnityIGCSConnector()
        {
            var lib = NativeMethods.LoadLibrary(@"UnityIGCSConnector.dll");
            if (lib == IntPtr.Zero) 
            {
                ExplorerCore.LogWarning("UnityIGCSConnector.dll was not found so IGCSDof will not be available");
                return;
            }

            var func = NativeMethods.GetProcAddress(lib, @"U_IGCS_Initialize");
            if (func == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException("Failed to find 'U_IGCS_Initialize' which means you can have a corrupt UnityIGCSConnector.dll.");
            }

            var initFunc = (U_IGCS_Initialize)Marshal.GetDelegateForFunctionPointer(func, typeof(U_IGCS_Initialize));

            delegates.Add(new MoveCameraCallback(MoveCamera));
            delegates.Add(new SessionCallback(StartSession));
            delegates.Add(new SessionCallback(EndSession));

            CameraStatus = initFunc((MoveCameraCallback)delegates[0], (SessionCallback)delegates[1], (SessionCallback)delegates[2]);
            isValid = true;
        }
    }
}
