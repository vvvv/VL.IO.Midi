using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VL.Lib.Collections;
using Sanford.Multimedia.Midi;
using System.Reactive.Linq;
using VL.Lib.Reactive;
using SanfordInputDevice = Sanford.Multimedia.Midi.InputDevice;
using SanfordOutputDevice = Sanford.Multimedia.Midi.OutputDeviceBase;
using VL.Core;

namespace VL.Lib.IO.Midi
{

    [Serializable]
    public class MidiInputDevice : DynamicEnumBase<MidiInputDevice, MidiInputDeviceDefinition>
    {
        public MidiInputDevice(string value)
            : base(value)
        {
        }

        public static MidiInputDevice CreateDefault()
            => CreateDefaultBase("No input device found");      
    }

    public class MidiInputDeviceDefinition : DynamicEnumDefinitionBase<MidiInputDeviceDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        //actual work
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            
            var devices = SanfordInputDevice.DeviceCount;
            var deviceMap = new Dictionary<string, object>(devices);
            var deviceCountPerName = new Dictionary<string, int>();
            for (int i = 0; i < devices; i++)
            {
                var originalName = SanfordInputDevice.GetDeviceCapabilities(i).name;
                var deviceName = originalName;
                int nameCount;
                //if name was seen already, increase count and add number
                if(deviceCountPerName.TryGetValue(originalName, out nameCount))
                {
                    nameCount++;
                    deviceName += " #" + nameCount;
                }
                else
                {
                    nameCount = 1;
                }
                deviceCountPerName[originalName] = nameCount;
                deviceMap[deviceName] = i;
            }

            return deviceMap;
        }
    }

    [Serializable]
    public class MidiOutputDevice : DynamicEnumBase<MidiOutputDevice, MidiOutputDeviceDefinition>
    {
        public MidiOutputDevice(string value)
            : base(value)
        {
        }

        public static MidiOutputDevice CreateDefault()
            => CreateDefaultBase("No output device found");
    }

    public class MidiOutputDeviceDefinition : DynamicEnumDefinitionBase<MidiOutputDeviceDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        //actual work
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var devices = SanfordOutputDevice.DeviceCount;
            var deviceMap = new Dictionary<string, object>(devices);
            var deviceCountPerName = new Dictionary<string, int>();
            for (int i = 0; i < devices; i++)
            {
                var originalName = SanfordOutputDevice.GetDeviceCapabilities(i).name;
                var deviceName = originalName;
                int nameCount;
                //if name was seen already, increase count and add number
                if (deviceCountPerName.TryGetValue(originalName, out nameCount))
                {
                    nameCount++;
                    deviceName += " #" + nameCount;
                }
                else
                {
                    nameCount = 1;
                }
                deviceCountPerName[originalName] = nameCount;
                deviceMap[deviceName] = i;
            }

            return deviceMap;
        } 
    }
}
