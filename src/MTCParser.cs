using Sanford.Multimedia.Midi;

namespace VL.Lib.IO.Midi
{
    public class MTCParser
    {
        private bool FInitialized = false;
        public int Frames = 0;
        private int Seconds = 0;
        private int Minutes = 0;
        private int Hours = 0;
        public double Time = 0;

        public void ReceiveMidiMessage(IMidiMessage msg)
        {
            byte SMPTEType, FPS, SMPTEPart;

            if (msg.MessageType == MessageType.SystemCommon)
            {
                var scm = msg as SysCommonMessage;

                if (scm != null && scm.SysCommonType == SysCommonType.MidiTimeCode)
                {
                    SMPTEPart = (byte)(scm.Data1 >> 4);
                    switch (SMPTEPart)
                    {
                        case 0:
                            Frames = scm.Data1 & 0x0F;
                            FInitialized = true;
                            break;
                        case 1:
                            Frames |= (scm.Data1 & 0x0F) << 4;
                            break;
                        case 2:
                            Seconds = scm.Data1 & 0x0F;
                            break;
                        case 3:
                            Seconds |= (scm.Data1 & 0x0F) << 4;
                            break;
                        case 4:
                            Minutes = scm.Data1 & 0x0F;
                            break;
                        case 5:
                            Minutes |= (scm.Data1 & 0x0F) << 4;
                            break;
                        case 6:
                            Hours = scm.Data1 & 0x0F;
                            break;
                        case 7:
                            Hours |= (scm.Data1 & 0x01) << 4;
                            SMPTEType = (byte)((scm.Data1 & 0x0F) >> 1);
                            switch (SMPTEType)
                            {
                                case 0:
                                    FPS = 24;
                                    break;
                                case 1:
                                    FPS = 25;
                                    break;
                                case 2:
                                    FPS = 30; // drop frame (doesn't matter here...)
                                    break;
                                case 3:
                                    FPS = 30;
                                    break;
                                default:
                                    FPS = 25;
                                    break;
                            }
                            if (FInitialized)
                                Time = Seconds + 60 * Minutes + 3600 * Hours;
                            break;
                    }
                }
            }
            else if (msg.MessageType == MessageType.SystemExclusive)
            {
                var sem = msg as SysExMessage;

                if (sem != null && 
                    sem.SysExType == SysExType.Start && 
                    sem.Length == 10 && 
                    sem[1] == 0x7F &&
                    sem[3] == 0x01 &&
                    sem[4] == 0x01 &&
                    sem[9] == 0xF7)
                    {
                        Frames = sem[8];
                        Seconds = sem[7];
                        Minutes = sem[6];
                        Hours = sem[5] & 0x0F;
                        SMPTEType = (byte)(sem[5] >> 5);
                        switch (SMPTEType)
                        {
                            case 0:
                                FPS = 24;
                                break;
                            case 1:
                                FPS = 25;
                                break;
                            case 2:
                                FPS = 30; // drop frame (doesn't matter here...)
                                break;
                            case 3:
                                FPS = 30;
                                break;
                            default:
                                FPS = 25;
                                break;
                        }
                        Time = Seconds + 60 * Minutes + 3600 * Hours;
                    }
            }
        }
    }
}
