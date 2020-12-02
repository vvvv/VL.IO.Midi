using System;
using Sanford.Multimedia.Midi;
using VL.Lib.Basics.Resources;
using System.Reactive.Linq;
using VL.Lib.Reactive;
using System.Runtime.CompilerServices;
using VL.Lib.Primitive;

namespace VL.Lib.IO.Midi
{
    public class MessageSplitter : MidiObservable
    {
        public static readonly MessageSplitter Default = new MessageSplitter(null);

        IObservable<IMidiMessage> FInput;

        private MessageSplitter(IObservable<IMidiMessage> input)
            : base(input)
        {
            FInput = input;
        }

        public MessageSplitter Update(IObservable<IMidiMessage> input,
            //out IObservable<ShortMessage> shortMessages,
            out IObservable<ChannelMessage> channelMessages,
            out IObservable<SysCommonMessage> commonMessages,
            out IObservable<SysRealtimeMessage> realtimeMessages,
            out IObservable<SysExMessage> sysExMessages)
        {
            if (input == FInput)
            {
                //shortMessages = ShortMessages;
                channelMessages = ChannelMessages;
                commonMessages = CommonMessages;
                realtimeMessages = RealtimeMessages;
                sysExMessages = SysExMessages;
                return this;
            }

            return new MessageSplitter(input)
                .Update(input, /*out shortMessages,*/ out channelMessages, out commonMessages, out realtimeMessages, out sysExMessages);
        }
    }

    public class ChannelMessageSplitter
    {
        public static readonly ChannelMessageSplitter Default = new ChannelMessageSplitter(null);

        readonly IObservable<IMidiMessage> FInput;
        
        readonly IObservable<ChannelMessage> FNoteMessages;
        readonly IObservable<ChannelMessage> FControllerMessages;
        readonly IObservable<ChannelMessage> FPitchWheelMessages;
        readonly IObservable<ChannelMessage> FModulationWheelMessages;

        private ChannelMessageSplitter(IObservable<IMidiMessage> input)
        {
            FInput = input;

            if (FInput != null)
            {
                var channelMessages = FInput as IObservable<ChannelMessage>;

                if (channelMessages == null)
                    channelMessages = FInput.OfType<ChannelMessage>()
                        .PubRefCount();

                FNoteMessages = channelMessages
                    .Where(m => m.Command == ChannelCommand.NoteOn || m.Command == ChannelCommand.NoteOff)
                    .PubRefCount();

                FControllerMessages = channelMessages
                    .Where(m => m.Command == ChannelCommand.Controller)
                    .PubRefCount();

                FPitchWheelMessages = channelMessages
                   .Where(m => m.Command == ChannelCommand.PitchWheel)
                   .PubRefCount(); 

                FModulationWheelMessages = channelMessages
                    .Where(m => m.Command == ChannelCommand.Controller && m.Data1 == 1)
                    .PubRefCount();
            }
        }

        public ChannelMessageSplitter Update(IObservable<IMidiMessage> input,
            out IObservable<ChannelMessage> noteMessages,
            out IObservable<ChannelMessage> controllerMessages,
            out IObservable<ChannelMessage> pitchWheelMessages,
            out IObservable<ChannelMessage> modulationWheelMessages)
        {
            if (input == FInput)
            {
                noteMessages = FNoteMessages;
                controllerMessages = FControllerMessages;
                pitchWheelMessages = FPitchWheelMessages;
                modulationWheelMessages = FModulationWheelMessages;
                return this;
            }

            return new ChannelMessageSplitter(input)
                .Update(input, out noteMessages, out controllerMessages, out pitchWheelMessages, out modulationWheelMessages);
        }
    }

    public static class MessageUtils
    {
        public static readonly ShortMessage ShortMessageDefault = new ShortMessage(176, 0, 0);

        public static readonly ChannelMessage ChannelMessageDefault = new ChannelMessage(ChannelCommand.Controller, 0, 0, 0);

        public static readonly SysCommonMessage SysCommonMessageDefault = new SysCommonMessage(SysCommonType.SongPositionPointer, 0, 0);

        public static readonly SysRealtimeMessage SysRealtimeMessageDefault = SysRealtimeMessage.ContinueMessage;

        public static readonly SysExMessage SysExMessageDefault = new SysExMessage(new byte[] { (byte)SysExType.Start });

        /// <summary>
        /// Converts an Integer32 in the range 0-127 to a Float32 in the range 0-1. Also takes care of clamping
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float MidiIntToFloat(int value)
        {
            return Clamp7Bit(value) / 127f;
        }

        /// <summary>
        /// Converts a Float32 in the range 0-1 to an Integer32 in the range 0-127. Also takes care of clamping
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int FloatToMidiInt(float value)
        {
            return Clamp7Bit(Float32Extensions.Round(value * 127));
        }

        /// <summary>
        /// Converts an Integer32 in the range 0-16383 to a Float32 in the range 0-1. Also takes care of clamping
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float MidiIntToFloat14(int value)
        {
            return Clamp14Bit(value) / 16383f;
        }

        /// <summary>
        /// Converts a Float32 in the range 0-1 to an Integer32 in the range 0-16383. Also takes care of clamping
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int FloatToMidiInt14(float value)
        {
            return Clamp14Bit(Float32Extensions.Round(value * 16383));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp7Bit(int value)
        {
            if (value > 127)
                return 127;

            if (value < 0)
                return 0;

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp14Bit(int value)
        {
            if (value > 16383)
                return 16383;

            if (value < 0)
                return 0;

            return value;
        }

        /// <summary>
        /// Checks whether the midi message is a note on of off message, on is NoteOn with velocity > 0, off is NoteOff or NoteOn with velocity = 0
        /// </summary>
        /// <param name="message"></param>
        /// <param name="isNoteOn"></param>
        /// <param name="isNoteOff"></param>
        public static void IsNoteOnOrOff(IMidiMessage message, out bool isNoteOn, out bool isNoteOff)
        {
            if (message.MessageType == MessageType.Channel)
            {
                var channelMessage = (ChannelMessage)message;
                var command = channelMessage.Command;

                if (command == ChannelCommand.NoteOn && channelMessage.Data2 > 0)
                {
                    isNoteOn = true;
                    isNoteOff = false;
                    return;
                }

                if (command == ChannelCommand.NoteOff || (channelMessage.Command == ChannelCommand.NoteOn && channelMessage.Data2 == 0))
                {
                    isNoteOn = false;
                    isNoteOff = true;
                    return;
                }
            }

            isNoteOn = false;
            isNoteOff = false;
        }

        //public static bool IsNoteOn(IMidiMessage message)
        //{
        //    if(message.MessageType == MessageType.Channel)
        //    {
        //        var channelMessage = (ChannelMessage)message;

        //        if(channelMessage.Command == ChannelCommand.NoteOn && channelMessage.Data2 > 0)
        //        {
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        //public static bool IsNoteOff(IMidiMessage message)
        //{
        //    if (message.MessageType == MessageType.Channel)
        //    {
        //        var channelMessage = (ChannelMessage)message;

        //        if (channelMessage.Command == ChannelCommand.NoteOff)
        //        {
        //            return true;
        //        }

        //        if (channelMessage.Command == ChannelCommand.NoteOn && channelMessage.Data2 == 0)
        //        {
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        public static ChannelMessage NoteOn(int channel, int noteNumber, float velocity)
        {
            return new ChannelMessage(ChannelCommand.NoteOn, channel, noteNumber, FloatToMidiInt(velocity));
        }

        public static ChannelMessage NoteOnInt(int channel, int noteNumber, int velocity)
        {
            return new ChannelMessage(ChannelCommand.NoteOn, channel, noteNumber, velocity);
        }

        public static ChannelMessage NoteOff(int channel, int noteNumber, float velocity)
        {
            return new ChannelMessage(ChannelCommand.NoteOff, channel, noteNumber, FloatToMidiInt(velocity));
        }

        public static ChannelMessage NoteOffInt(int channel, int noteNumber, int velocity)
        {
            return new ChannelMessage(ChannelCommand.NoteOff, channel, noteNumber, velocity);
        }

        /// <summary>
        /// Returns the corresponding NoteOff message to the given NoteOn message
        /// </summary>
        /// <param name="noteOnMessage"></param>
        /// <returns></returns>
        public static ChannelMessage NoteOff(ChannelMessage noteOnMessage)
        {
            return new ChannelMessage(ChannelCommand.NoteOff, noteOnMessage.MidiChannel, noteOnMessage.Data1, noteOnMessage.Data2);
        }

        /// <summary>
        /// Controller #120 with value 0
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static ChannelMessage AllSoundOff(int channel)
        {
            return new ChannelMessage(ChannelCommand.Controller, channel, 120, 0);
        }

        /// <summary>
        /// Controller #123 with value 0
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static ChannelMessage AllNotesOff(int channel)
        {
            return new ChannelMessage(ChannelCommand.Controller, channel, 123, 0);
        }

        public static ChannelMessage Controller(int channel, int controllerNumber, float value)
        {
            return new ChannelMessage(ChannelCommand.Controller, channel, controllerNumber, FloatToMidiInt(value));
        }

        public static ChannelMessage ControllerInt(int channel, int controllerNumber, int value)
        {
            return new ChannelMessage(ChannelCommand.Controller, channel, controllerNumber, value);
        }

        public static ChannelMessage PitchWheel(int channel, float value)
        {
            var intValue = FloatToMidiInt14(value);
            return new ChannelMessage(ChannelCommand.PitchWheel, channel, intValue & 0x7F, intValue >> 7 & 0x7F);
        }

        public static ChannelMessage PitchWheelInt(int channel, int value)
        {
            return new ChannelMessage(ChannelCommand.PitchWheel, channel, value & 0x7F, value >> 7 & 0x7F);
        }

        public static float GetPitchWheel(this ChannelMessage pitchWheelMessage)
        {
            return MidiIntToFloat14(pitchWheelMessage.Data2 << 7 | pitchWheelMessage.Data1);
        }

        public static int GetPitchWheelInt(this ChannelMessage pitchWheelMessage)
        {
            return pitchWheelMessage.Data2 << 7 | pitchWheelMessage.Data1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split(this ChannelMessage message, out ChannelCommand command, out int channel, out int number, out int value)
        {
            command = message.Command;
            channel = message.MidiChannel;
            number = message.Data1;
            value = message.Data2;
        }

        public static void SplitAs(this ChannelMessage message, ChannelCommand command, out bool success, out int channel, out int number, out int value)
        {
            if(message.Command == command)
            {
                success = true;
                message.Split(out command, out channel, out number, out value);
            }

            success = false;
            channel = 0;
            number = 0;
            value = 0;
        }

        public static string MessageToString(IMidiMessage message)
        {          
            switch (message.MessageType)
            {
                case MessageType.Channel:
                    var cm =(ChannelMessage)message;
                    return cm.Command.ToString() + " Ch: " +
                    cm.MidiChannel.ToString() + " Nr: " +
                    cm.Data1.ToString() + " Value: " +
                    cm.Data2.ToString();
                case MessageType.SystemExclusive:
                    var syx = (SysExMessage)message;
                    string result = "SysEx Message: ";
                    foreach (byte b in syx)
                        result += string.Format("{0:X2} ", b);
                    return result;
                case MessageType.SystemCommon:
                    var sym = (SysCommonMessage)message;
                    return sym.SysCommonType.ToString() + " Data 1: " +
                    sym.Data1.ToString() + " Data 2: " +
                    sym.Data2.ToString();
                case MessageType.SystemRealtime:
                    var rm = (SysRealtimeMessage)message;
                    return "Realtime Mesage: " + rm.SysRealtimeType.ToString();
                case MessageType.Meta:
                    return "Meta Message";
                case MessageType.Short:
                    var sm = (ShortMessage)message;
                    return "Short Message";
            }

            return message.ToString();
        }
    }
}