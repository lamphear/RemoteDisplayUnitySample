// Copyright 2015 Google Inc.

#if UNITY_IOS

using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/**
 * Bridges Unity with functions exposed by the iOS Google Cast Remote Display API native library.
 */
public class CastRemoteDisplayiOSUnityBridge {
  /**
   * Enumerates the supported remote display audio formats.
   * This must be kept in sync with AVAudioPCMFormat as defined in the AVFoundation framework.
   */
  public enum AudioFormat {
    AVAudioPCMFormatFloat32 = 1,
    AVAudioPCMFormatFloat64,
    AVAudioPCMFormatInt16,
    AVAudioPCMFormatInt32
  }

  /**
   * Specifies the configuration for setting up a remote display session for a selected cast device.
   * The order of the fields (alphabetical) and their types must be kept in sync with
   * RemoteDisplayConfigStruct in GCKUnityRemoteDisplayBridge.
   */
  [StructLayout(LayoutKind.Sequential)]
  public struct RemoteDisplayConfigStruct {
    public uint audioBitrate;
    public int frameRate;
    public int resolution;
    public int targetDelay;
    public uint videoBitrate;
  }

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnityStartScan(string applicationID, string scanListenerName);

  [DllImport ("__Internal")]
  private static extern IntPtr _native_GCKUnityGetCastDeviceInfoAsStringArrayPtr();

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnityFreeStringArrayPtr(IntPtr stringArrayPtr);

  [DllImport ("__Internal")]
  private static extern bool _native_GCKUnitySelectCastDevice(string deviceID,
      RemoteDisplayConfigStruct remoteDisplayConfigStruct);

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnitySetRemoteDisplayTexture(IntPtr texturePtr);

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnityRenderRemoteDisplay();

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnitySetAudioFormat(int audioFormat, int sampleRate,
      int numberChannels, bool isInterleaved);

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnityEnqueueRemoteDisplayAudioBuffer(float[] data,
      int dataByteSize, int numberChannels, int numberFrames);

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnityStopRemoteDisplaySession();

  [DllImport ("__Internal")]
  private static extern void _native_GCKUnityTeardownRemoteDisplay();

  /**
   * Initiates scan for devices that support given application ID. The native
   * library will send the "_callback_OnCastDevicesUpdated" message when the list of available cast
   * devices is updated.
   */
  public static void StartScan(string applicationID, MonoBehaviour scanListener) {
    _native_GCKUnityStartScan(applicationID, scanListener.name);
  }

  /**
   * Connect to a specified device and start a remote display session.
   */
  public static bool SelectCastDevice(string deviceID, CastRemoteDisplayConfiguration config) {
    RemoteDisplayConfigStruct remoteDisplayConfigStruct;
    remoteDisplayConfigStruct.audioBitrate = Convert.ToUInt32(config.audioBitrate);
    remoteDisplayConfigStruct.frameRate = Convert.ToInt32(config.frameRate);
    remoteDisplayConfigStruct.resolution = Convert.ToInt32(config.resolution);
    remoteDisplayConfigStruct.targetDelay = Convert.ToInt32(config.targetDelay);
    remoteDisplayConfigStruct.videoBitrate = Convert.ToUInt32(config.videoBitrate);
    return _native_GCKUnitySelectCastDevice(deviceID, remoteDisplayConfigStruct);
  }

  /**
   * Returns devices available to start a remote display session with. Assumes #StartScan was
   * called.
   */
  public static List<CastDevice> GetCastDevices() {
    // Retrieve string array pointer, copy to C# managed string array, and then free pointer.
    IntPtr deviceInfoStringArrayPtr = _native_GCKUnityGetCastDeviceInfoAsStringArrayPtr();
    string[] deviceInfoStrings = getStringArrayFromNativePointer(deviceInfoStringArrayPtr);
    _native_GCKUnityFreeStringArrayPtr(deviceInfoStringArrayPtr);

    // The static library will respond with triplets of strings representing a cast device. The
    // order is deviceId, deviceName, and the status.
    List<CastDevice> devices = new List<CastDevice>();
    int i = 0;
    while (i < deviceInfoStrings.Length - 2) {
      CastDevice device = new CastDevice();
      device.deviceId = deviceInfoStrings[i++];
      device.deviceName = deviceInfoStrings[i++];
      device.status = deviceInfoStrings[i++];
      devices.Add(device);
    }
    return devices;
  }

  /**
   * Sets the texture pointer to render the cast remote display with.
   */
  public static void SetRemoteDisplayTexture(IntPtr texturePtr) {
    _native_GCKUnitySetRemoteDisplayTexture(texturePtr);
  }

  /**
   * Renders the texture to the cast remote display. Assumes #SetRemoteDisplayTexture was called.
   */
  public static void RenderRemoteDisplay() {
    _native_GCKUnityRenderRemoteDisplay();
  }

  /**
   * Sets the audio format used for audio data to enqueue and play on cast remote display.
   */
  public static void SetAudioFormat(AudioFormat audioFormat, int sampleRate, int numberChannels,
    bool isInterleaved) {
    _native_GCKUnitySetAudioFormat((int)audioFormat, sampleRate, numberChannels, isInterleaved);
  }

  /**
   * Enqueue audio buffer for playback on cast remote display. Assumes #SetAudioFormat was called.
   */
  public static void EnqueueRemoteDisplayAudioBuffer(float[] data, int dataByteSize,
      int numberChannels, int numberFrames) {
      _native_GCKUnityEnqueueRemoteDisplayAudioBuffer(data, dataByteSize, numberChannels,
          numberFrames);
  }

  /**
   * Stops the current remote display session. This can be used in the middle of the game to let the
   * user stop and disconnect and later select another Cast device.
   */
  public static void StopRemoteDisplaySession() {
    _native_GCKUnityStopRemoteDisplaySession();
  }

  /**
   * Stops the curent remote display session and stops scanning for updates. Call #StartScan to set
   * everything up again. This should only be used when deactivating, shutting down, backgrounding
   * the game.
   */
  public static void TeardownRemoteDisplay() {
    _native_GCKUnityTeardownRemoteDisplay();
  }

  /**
   * Helper that returns a C# managed string array from a null-terminated pointer array.
   */
  private static string[] getStringArrayFromNativePointer(IntPtr charStringArray) {
    if (charStringArray == IntPtr.Zero) {
      return new string[0];
    }

    int stringArrayLength = 0;
    while (Marshal.ReadIntPtr(charStringArray, stringArrayLength * IntPtr.Size) != IntPtr.Zero) {
      stringArrayLength++;
    }

    string[] stringArray = new string[stringArrayLength];
    for (int i = 0; i < stringArrayLength; i++) {
      IntPtr charString = Marshal.ReadIntPtr(charStringArray, i * IntPtr.Size);
      if (charString == IntPtr.Zero) {
        stringArray[i] = "";
      } else {
        stringArray[i] = Marshal.PtrToStringAnsi(charString);
      }
    }
    return stringArray;
  }
}

#endif
