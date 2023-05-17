using Sanford.Multimedia.Midi;
using System;
using VL.Core;
using System.Reactive.Linq;
using VL.Lib.Basics.Resources;
using SanfordOutputDevice = Sanford.Multimedia.Midi.OutputDevice;
using VL.Lib.Reactive;
using VL.Lib.Collections;

namespace VL.Lib.IO.Midi
{
    public class MidiOut : IDisposable
    {
        string FDeviceName;
        MidiOutEventSink OutputEventSink;

        public MidiOut()
        {   
        }

        public void Update(IObservable<IMidiMessage> messages, IObservable<IMidiMessage> messagesDefaultHack, MidiOutputDevice device, out bool isOpen)
        {
            if (device.IsValid())
            {
                if (device.Value != FDeviceName)
                {
                    var deviceID = (int)device.Tag;
                    FDeviceName = device.Value;

                    ReleaseDevice();
                    OutputEventSink = new MidiOutEventSink(deviceID);
                }
            }
            else
            {
                FDeviceName = null;
                ReleaseDevice();
            }

            if (OutputEventSink != null)
                OutputEventSink.SetObservable(messages, messagesDefaultHack);

            isOpen = OutputEventSink?.IsOpen ?? false;
        }

        private void ReleaseDevice()
        {
            if (OutputEventSink != null)
            { 
                OutputEventSink.Dispose();
                OutputEventSink = null;
            }
        }

        public void Dispose()
        {
            this.ReleaseDevice();
        }
    }

    /// <summary>
    /// Event sink that sends midi messages to an output device
    /// </summary>
    public class MidiOutEventSink : ObservableInputBase<IMidiMessage>
    {
        IResourceHandle<SanfordOutputDevice> FOutDeviceHandle;
        IResourceProvider<SanfordOutputDevice> FOutDeviceProvider;
        bool FInputCompleted;

        public bool IsOpen => FOutDeviceHandle != null;

        public MidiOutEventSink(int deviceID)
        {
            var deviceCount = OutputDeviceBase.DeviceCount;
            if (deviceCount > 0)
            {
                deviceID %= deviceCount;
                FOutDeviceProvider = ResourceProvider.NewPooledSystemWide(deviceID, id => new SanfordOutputDevice(id));
            }   
        }

        void EnsureDeviceOpen()
        {
            if(FOutDeviceHandle == null)
            {
                FOutDeviceHandle = FOutDeviceProvider.GetHandle();
            }
        }

        void CloseDevice()
        {
            if (FOutDeviceHandle != null)
            {
                FOutDeviceHandle.Dispose();
                FOutDeviceHandle = null; 
            }
        }

        public void SetObservable(IObservable<IMidiMessage> messages, IObservable<IMidiMessage> messagesDefaultHack)
        {
            CustomManageObservable(messages, messagesDefaultHack);

            //close device if all observables are completed
            if (FInputCompleted)
            {
                CloseDevice();
            }
        }

        /// <summary>
        /// Subscribes to the observable if not already.
        /// If the input is null of the default only the old subscription is disposed.
        /// </summary>
        /// <param name="observable">The potentially new observable.</param>
        /// <param name="observableDefaultHack">Default observable as seen by VL.</param>
        protected void CustomManageObservable(IObservable<IMidiMessage> observable, IObservable<IMidiMessage> observableDefaultHack)
        {
            if (observable != FObservable)
            {
                FInputCompleted = true;
                FSubscription.Disposable = null;
                FObservable = observable;

                if (observable != null && observable != observableDefaultHack && !(observable is DefaultMidiObservable))
                {
                    EnsureDeviceOpen();
                    FInputCompleted = false;
                    FSubscription.Disposable = observable.Subscribe(
                        OnNext,
                        _ => FInputCompleted = true,
                        () => FInputCompleted = true);

                }            
            }
        }

        protected override void OnNext(IMidiMessage input)
        {
            try
            {
                switch (input.MessageType)
                {
                    case MessageType.Channel:
                    case MessageType.SystemCommon:
                    case MessageType.SystemRealtime:
                    case MessageType.Meta:
                    case MessageType.Short:
                        FOutDeviceHandle?.Resource.SendShort(((ShortMessage)input).Message);
                        break;
                    case MessageType.SystemExclusive:
                        FOutDeviceHandle?.Resource.Send((SysExMessage)input);
                        break;
                }

                var hash = this.GetHashCode();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
            }
        }

        protected override void ReleaseResources()
        {
            // Unscubscribe first before disposing the state. This is very important in case the OnNext call is on a different thread.
            base.ReleaseResources();
            //only dispose the handle, the actual device will be desposed
            //by the resource handler pool onced all handes are closed.
            CloseDevice();
        }
    }
}
