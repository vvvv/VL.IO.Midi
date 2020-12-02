using Sanford.Multimedia.Midi;
using System;
using System.Linq;
using System.Reactive.Linq;
using VL.Lib.Basics.Resources;
using VL.Lib.Reactive;
using VL.Lib.Collections;

namespace VL.Lib.IO.Midi
{
    public class MidiIn
    {
        string FDeviceName;
        MidiObservable MidiInObservables;
        Func<bool> CheckIsOpen;

        public MidiIn()
        {
            
        }

        public IObservable<IMidiMessage> Update(MidiInputDevice device, out bool isOpen)
        {
            if (device.IsValid())
            {
                if (device.Value != FDeviceName)
                {
                    var deviceID = (int)device.Tag;
                    FDeviceName = device.Value;
                    var midiIn = new MidiInObservable(deviceID);
                    MidiInObservables = midiIn;
                    CheckIsOpen = () => midiIn.IsOpen;
                }
            }
            else
            {
                FDeviceName = null;
                MidiInObservables = MidiObservableUtils.DefaultMidiObservable;
                CheckIsOpen = () => false;
            }

            isOpen = CheckIsOpen();
            return MidiInObservables;
        }
    }

    /// <summary>
    /// Base class for several midi classes that provide midi mesages, like:
    /// Input device, message splitter or a dummy if no input device was found.
    /// </summary>
    public class MidiObservable : IObservable<IMidiMessage>
    {
        //MidiEvents FEventSource;
        public readonly IObservable<IMidiMessage> Messages;
        public readonly IObservable<ShortMessage> ShortMessages;
        public readonly IObservable<ChannelMessage> ChannelMessages;
        public readonly IObservable<SysCommonMessage> CommonMessages;
        public readonly IObservable<SysRealtimeMessage> RealtimeMessages;
        public readonly IObservable<SysExMessage> SysExMessages;

        public MidiObservable(IObservable<IMidiMessage> messages)
        {
            if (messages != null)
            {
                var otherMidiObservable = messages as MidiObservable;
                if (otherMidiObservable != null)
                {
                    Messages = otherMidiObservable.Messages;
                    ShortMessages = otherMidiObservable.ShortMessages;
                    ChannelMessages = otherMidiObservable.ChannelMessages;
                    CommonMessages = otherMidiObservable.CommonMessages;
                    RealtimeMessages = otherMidiObservable.RealtimeMessages;
                    SysExMessages = otherMidiObservable.SysExMessages;
                }
                else
                {
                    Messages = messages;
                    ShortMessages = Messages.OfType<ShortMessage>().PubRefCount();
                    ChannelMessages = Messages.OfType<ChannelMessage>().PubRefCount();
                    CommonMessages = Messages.OfType<SysCommonMessage>().PubRefCount();
                    RealtimeMessages = Messages.OfType<SysRealtimeMessage>().PubRefCount();
                    SysExMessages = Messages.OfType<SysExMessage>().PubRefCount();
                }
            }
            else //event source is null
            {
                Messages = MidiObservableUtils.DefaultMessageObservable;
                ShortMessages = MidiObservableUtils.DefaultShortObservable;
                ChannelMessages = MidiObservableUtils.DefaultChannelObservable;
                CommonMessages = MidiObservableUtils.DefaultCommonObservable;
                RealtimeMessages = MidiObservableUtils.DefaultRealtimeObservable;
                SysExMessages = MidiObservableUtils.DefaultSysExObservable;
            }
        }

        public MidiObservable(IResourceProvider<MidiEvents> eventSource)
        {
            if (eventSource != null)
            {
                Messages = Observable.Using(
                    () => eventSource.GetHandle(),
                    midiEventsHandle => Observable.FromEvent<MidiMessageEventHandler, IMidiMessage>(
                        h => midiEventsHandle.Resource.MessageReceived += h,
                        h => midiEventsHandle.Resource.MessageReceived -= h))
                        .PubRefCount();

                ShortMessages = Messages.OfType<ShortMessage>().PubRefCount();
                ChannelMessages = Messages.OfType<ChannelMessage>().PubRefCount();
                CommonMessages = Messages.OfType<SysCommonMessage>().PubRefCount();
                RealtimeMessages = Messages.OfType<SysRealtimeMessage>().PubRefCount();
                SysExMessages = Messages.OfType<SysExMessage>().PubRefCount();

                //ChannelMessages = Observable.Using(
                //    () => eventSource.GetHandle(),
                //    midiEventsHandle => Observable.FromEventPattern<ChannelMessageEventArgs>(
                //        h => midiEventsHandle.Resource.ChannelMessageReceived += h,
                //        h => midiEventsHandle.Resource.ChannelMessageReceived -= h)
                //        .Select(x => x.EventArgs.Message)).PubRefCount();
            }
            else //event source is null
            {
                Messages = MidiObservableUtils.DefaultMessageObservable;
                ShortMessages = MidiObservableUtils.DefaultShortObservable;
                ChannelMessages = MidiObservableUtils.DefaultChannelObservable;
                CommonMessages = MidiObservableUtils.DefaultCommonObservable;
                RealtimeMessages = MidiObservableUtils.DefaultRealtimeObservable;
                SysExMessages = MidiObservableUtils.DefaultSysExObservable;
            }
        }

        public IDisposable Subscribe(IObserver<IMidiMessage> observer)
        {
            return Messages.Subscribe(observer);
        }
    }

    public class MidiInObservable : MidiObservable
    {
        private readonly int FDeviceID;

        public MidiInObservable(int deviceID)
           : base(CreateMidiInResourceProvider(deviceID))
        {
            FDeviceID = deviceID;
        }

        public bool IsOpen => ResourceProvider.ExistsInSystemWideProviderPool<int, InputDeviceMidiEvents>(FDeviceID);

        static IResourceProvider<InputDeviceMidiEvents> CreateMidiInResourceProvider(int deviceID)
        {
            return ResourceProvider.NewPooledSystemWide(deviceID, id =>
            {
                var midiInDevice = new InputDevice(id, false);
                return new InputDeviceMidiEvents(midiInDevice);
            })
            .ShareInParallel(delayDisposalInMilliseconds: 250);
        }
    }

    /// <summary>
    /// The default event provider used by the MidiIn node
    /// if the midi input device could not be opened.
    /// </summary>
    /// <seealso cref="VL.Lib.IO.Midi.MidiObservable" />
    public class DefaultMidiObservable : MidiObservable
    {
        public DefaultMidiObservable()
            : base(default(IObservable<IMidiMessage>))
        {
        }
    }

    public static class MidiObservableUtils
    {
        public static readonly IObservable<IMidiMessage> DefaultMessageObservable = Observable.Empty<IMidiMessage>();

        public static readonly IObservable<ShortMessage> DefaultShortObservable = Observable.Empty<ShortMessage>();

        public static readonly IObservable<ChannelMessage> DefaultChannelObservable = Observable.Empty<ChannelMessage>();

        public static readonly IObservable<SysCommonMessage> DefaultCommonObservable = Observable.Empty<SysCommonMessage>();

        public static readonly IObservable<SysRealtimeMessage> DefaultRealtimeObservable = Observable.Empty<SysRealtimeMessage>();

        public static readonly IObservable<SysExMessage> DefaultSysExObservable = Observable.Empty<SysExMessage>();

        public static readonly MidiObservable DefaultMidiObservable;

        static MidiObservableUtils()
        {
            DefaultMidiObservable = new DefaultMidiObservable();
        }
    }
}
