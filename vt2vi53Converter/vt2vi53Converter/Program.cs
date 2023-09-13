using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace vt2vi53Converter
{
    class Program
    {
        
        public static byte getByte0V(char chr)
        {
            byte num = (byte)chr;

            if (num>64)
            {
                return (byte)(num - 65 + 10);
            }
            else
            {
                return (byte)(num - 48);
            }
        }


        public static void AppendAllBytes(string path, byte[] bytes)
        {
            //argument-checking here.

            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        static void Main(string[] args)
        {
            
            const byte cmdKeepPlayin = 00;
            const byte cmdSpeed = 0x7f;
            const byte cmdMuteCh = 0x7e;
            const byte cmdPatChange = 0x7d;
            const byte cmdOrnament = 0x7c;
            const byte cmdOrnamentOffset = 0x7b;
            const byte cmdSample = 0x7a;
            const byte cmdSampleOffset = 0x79;

            if (args.Length <=1)
            {                
                Console.WriteLine("First argument - input vt2 file, second - number of Channels(1,2,3)");
                return;
            }


            int channels = int.Parse(args[1]);

            float chipFreq = 1500000;

            byte[] header = new byte[16];

            byte[] freqTable = new byte[256];

            //struct TONE-enabled, NOISE-enable,TONEVALUE,NOISE VOLUME,VOLUME
            //List<byte> patterns = new List<byte>();
            // byte[] patterns = new byte[256];
            // byte[] playOrder = new byte[128];
            List<byte> patterns = new List<byte>();
            List<byte> playOrder = new List<byte>();
            List<byte> notes = new List<byte>();

            //список орнаментов
            //первые 64 байта - описатели орнаментов, по 4 байта на орнамент
            // Табличка с описаниекм Орнаментов
            // dup 16
            // dw 00000; ornament data addr delta +64 для нулевого а дальше по длине других - всё в препроцессоре
            // db 0; loop position
            // db 0; end position(length) -when reach it loop to loop position
            // edup
            //потом сами орнаменты
            List<byte> ornaments = new List<byte>();
            int currentOrnament = 1;
            //prefill with zero
            for (int i=0;i<64;i++)
            {
                ornaments.Add(0);
            }


            //список инструментов
            //первые 128 байт - описатели инструментов, по 4 байта на инструмент
            //всё как у орнаментов
            // dup 32
            // dw 00000; 
            // db 0; loop position
            // db 0; end position(length) -when reach it loop to loop position
            // edup
            //потом сами инструменты
            //в них просто по два байта лежит, это дельта к частоте, если тона нет то частота 0000 тупо, т.к. накопление не поддерживается
            //т.к. значаших 3 байта то потом можно под маску тона 4-й старший задействовать или ещё под чё %)

            List<byte> samples = new List<byte>();
            int currentSample = 1;
            //prefill with zero
            for (int i = 0; i < 128; i++)
            {
                samples.Add(0);
            }


            string[] vt2File = File.ReadAllLines(args[0]);

            //find chipFreq to precalc notes
            foreach (string s in vt2File)
            {
                if (s.StartsWith("ChipFreq"))
                {
                    chipFreq = float.Parse(s.Split('=')[1]);
                }
            }

            Console.WriteLine("ChipFreq=" + chipFreq);

                //read notes freq from csv
                string[] toneCSV = File.ReadAllLines("ToneTables.csv");
            //build notes dictionary - note name - key
            //note - freq
            Dictionary<string, string> notesDictionary = new Dictionary<string, string>();
            //note - index
            Dictionary<string, byte> notesIndexes = new Dictionary<string, byte>();
            byte index = 0;
            foreach (string s in toneCSV)
            {
                string[] split = s.Split(';');

                notesDictionary.Add(split[0], split[2]);
                notesIndexes.Add(split[0], index);

                //build freq table
                if (index>0)
                 {
                    float toneFloat = Int32.Parse(split[2]) * 16.0f * (chipFreq / 1750000);
                    //Console.WriteLine(toneFloat);
                    freqTable[index  * 2+0] = (byte)(Math.Round(toneFloat) % 256);
                    freqTable[index * 2 + 1] = (byte)(Math.Round(toneFloat) / 256);
                }

                index++;

            }
            
           
    
            bool wasMute1 = true;
            bool wasMute2 = true;
            bool wasMute3 = true;            

            int currentPattern = 0;
            byte speed = 3;

            bool nextLineOrnament = false;
            bool nextLineSample = false;
            byte sampleCount = 0;//счётчик по семплу

            foreach (string s in vt2File)
            {

                //fill ornaments
                if (nextLineOrnament)
                {                    
                    //12,12,L0                    
                    //parse ornament
                    //Сохраняем адрес в дескриптов
                    //адрес текущего орнамента, это сохраненных байт колличиство
                    int addr = ornaments.Count();
                    ornaments[currentOrnament * 4 + 0] = (byte)(addr % 256);
                    ornaments[currentOrnament * 4 + 1] = (byte)(addr / 256);
                    //добавляем значения дельт
                    byte loopPos = 0;
                    string[] orz = s.Split(',');
                    for (byte i=0;i<orz.Length;i++)
                    {
                        byte ornaByte = 0;

                        if (orz[i].StartsWith("L"))
                            {
                                loopPos = i;
                                //sbyte bt = sbyte.Parse(orz[i].Trim('L'));                                )
                                ornaByte = (byte)sbyte.Parse(orz[i].Trim('L'));                                
                                //ornaments.Add(sbyte.Parse(orz[i].Trim('L')));
                            }
                        else
                        {
                            ornaByte = (byte)sbyte.Parse(orz[i]);
                            //int bt = int.Parse(orz[i].Trim('L'));
                            //ornaments.Add(syte.Parse(orz[i]));
                        }

                        ornaments.Add(ornaByte);
                    }

                    ornaments[currentOrnament * 4 + 2] = loopPos;
                    ornaments[currentOrnament * 4 + 3] = (byte)(orz.Length);

                    //reset flag
                    nextLineOrnament =false;
                    currentOrnament++;
                }
                if (s.Contains("Ornament"))
                {                    
                    nextLineOrnament = true;
                }


                //fill samples 
                //[Sample1]
                //T..+002_ + 00_ F_ L
                //T..+003_ + 00_ F_
                //T..+002_ + 00_ F_
                //T..-002_ + 00_ F_
                //T..-003_ + 00_ F_
                //T..-002_ + 00_ F_

                if (nextLineSample)
                {

                    //конец сэмпла
                    if (s.Length == 0)
                    {
                        //Console.WriteLine("deltaz:" + sampleCount);
                        //Console.WriteLine("loop:" + samples[currentSample * 4 + 2]);
                        nextLineSample = false;
                        currentSample++;
                    }
                    else
                    {
                        //save loop pos
                        if (s.EndsWith("L"))
                        {
                            samples[currentSample * 4 + 2] = (byte)(sampleCount);
                        }

                        //если есть тон в маске то сохраняем смешение частоны
                        if (s.StartsWith("T"))
                        {
                            string delta = s.Substring(5, 3);
                            int deltaInt = int.Parse(delta, System.Globalization.NumberStyles.HexNumber);


                            float deltaFloat = deltaInt * 16.0f * 0.8571428571428571f;

                            int realDelta = (int)Math.Round(deltaFloat);


                            //if sign "-"
                            if (s[4]=='-')
                            {
                                realDelta = 65536-realDelta;
                            }
                            samples.Add((byte)(realDelta % 256));
                            samples.Add((byte)(realDelta / 256));
                            //Console.WriteLine(deltaInt);
                        }
                        //иначе тупо 0
                        else
                        {
                            samples.Add(0);
                            samples.Add(0);
                        }

                        sampleCount++;
                        samples[currentSample * 4 + 3] = (byte)(sampleCount);
                    }

                }
                if (s.Contains("Sample"))
                {
                    nextLineSample = true;
                    sampleCount = 0;
                    int addr = samples.Count();
                    samples[currentSample * 4 + 0] = (byte)(addr % 256);
                    samples[currentSample * 4 + 1] = (byte)(addr / 256);
                }

                //get speed
                if (s.Contains("Speed"))
                {
                    speed = byte.Parse(s.Split('=')[1]);
                }

                    //fill playOrder
                    if (s.Contains("PlayOrder"))
                {
                    byte count = 0;
                    byte loopByte=128;//loop to 0 pattern
                    string[] order = (s.Split('='))[1].Split(',');
                    for (int i=0;i<order.Length;i++)
                    {
                        if (order[i].StartsWith("L"))
                        {
                            loopByte = count;
                            //playOrder[i] = (byte)(byte.Parse(order[i].Trim('L')));
                            playOrder.Add((byte)(byte.Parse(order[i].Trim('L'))));
                            
                        }
                        else
                        {
                            //playOrder[i] = byte.Parse(order[i]);
                            playOrder.Add(byte.Parse(order[i]));
                        }
                        count++;
                    }

                    //playOrder[count] = (byte)(loopByte+128);
                    playOrder.Add((byte)(loopByte + 128));
                    //svae speed
                    //playOrder[127] = speed;
                    header[10] = speed;

                }

                    //wait paterns section start
                    if (s.Contains("Pattern"))                                   
                {
                    //save start pattern addr to patterns list

                    //if (currentPattern > 0)
                    {
                        notes.Add(cmdPatChange);
                        patterns.Add((byte)((notes.Count) % 256));
                        patterns.Add((byte)((notes.Count) / 256));

                        //patterns[currentPattern * 2 + 0] = (byte)((notes.Count) % 256);
                        //patterns[currentPattern * 2 + 1] = (byte)((notes.Count) / 256);
                    }


                    ///
                    currentPattern++;
                }
                else
                {
                    if (currentPattern>0)
                    {

                        string[] patSplit = s.Split('|');



                        //coorect pattern
                        if (patSplit.Length == 5)
                        {

                            //full string for channel                            
                            string ch1 = patSplit[2];
                            string ch2 = patSplit[3];
                            string ch3 = patSplit[4];
                            


                            //==============================================================
                            //get speed command
                            //комманда скорости глобальная
                            string command1 = ch1.Split(' ')[2];

                            if (command1[0]=='B')
                            {
                                //put speed command
                                notes.Add(cmdSpeed);
                                //put new speed                                    
                                string speedValue = command1.Substring(2, 2).Replace('.', '0');
                                byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                //put speed value
                                notes.Add(speedByte);
                            }
                           
                            //орнамент
                            string QF1E_1 = ch1.Split(' ')[1];
                            //номер орнамента для включения
                            char orNumber_1 = QF1E_1[2];
                            if (orNumber_1 != '.')
                            {
                                byte orByte = byte.Parse(orNumber_1.ToString(), System.Globalization.NumberStyles.HexNumber);
                                //put start ornament command
                                notes.Add(cmdOrnament);
                                //number of ornament to apply
                                notes.Add(orByte);
                                //Console.WriteLine(orByte);
                            }


                            //ornament ofsett
                            if (command1[0] == '5')
                            {
                                //put speed command
                                notes.Add(cmdOrnamentOffset);
                                //put new speed                                    
                                string speedValue = command1.Substring(2, 2).Replace('.', '0');
                                byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                //put speed value
                                notes.Add(speedByte);
                            }

                            

                            //номер инструмента для включения                            
                            char samNumber_1 = QF1E_1[0];
                            if (samNumber_1 != '.')
                            {
                                byte samByte = getByte0V(samNumber_1); //byte.Parse(samNumber_1.ToString(), System.Globalization.NumberStyles.HexNumber);
                                //Console.WriteLine(samByte);
                                //put start ornament command
                                notes.Add(cmdSample);
                                //number of ornament to apply
                                notes.Add(samByte);
                                //Console.WriteLine(orByte);
                            }


                            //instrument offset
                            if (command1[0] == '4')
                            {
                                //put speed command
                                notes.Add(cmdSampleOffset);
                                //put new speed                                    
                                string speedValue = command1.Substring(2, 2).Replace('.', '0');
                                byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                //put speed value
                                notes.Add(speedByte);
                            }

                            //get one note
                            string note = ch1.Split(' ')[0];                          

                            if (notesDictionary.ContainsKey(note))
                            {                                                       
                                byte curNote = notesIndexes[note];//get note index

                                if (wasMute1)
                                {
                                    wasMute1 = false;
                                    //add enable channel command
                                    curNote += 128;
                                }
                                notes.Add(curNote);                                                                    
                            }
                            else
                            {
                                switch (note)
                                {
                                    // R--
                                    case "R--":
                                        //disable channel
                                        notes.Add(cmdMuteCh);
                                        wasMute1 = true;

                                        break;
                                    // ---
                                    default:
                                        //keep playin
                                        notes.Add(cmdKeepPlayin);
                                        break;
                                }

                            }
                            //=======================================================


                            //==============================================================
                            if (channels >= 2)
                            {
                                //комманда скорости глобальная
                                string command2 = ch2.Split(' ')[2];

                                if (command2[0] == 'B')
                                {
                                    //put speed command
                                    notes.Add(cmdSpeed);
                                    //put new speed                                    
                                    string speedValue = command2.Substring(2, 2).Replace('.', '0');
                                    byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                    //put speed value
                                    notes.Add(speedByte);
                                }




                                //орнамент
                                string QF1E_2 = ch2.Split(' ')[1];
                                //номер орнамента для включения
                                char orNumber_2 = QF1E_2[2];
                                if (orNumber_2 != '.')
                                {
                                    byte orByte = byte.Parse(orNumber_2.ToString(), System.Globalization.NumberStyles.HexNumber);
                                    //put start ornament command
                                    notes.Add(cmdOrnament);
                                    //number of ornament to apply
                                    notes.Add(orByte);
                                    //Console.WriteLine(orByte);
                                }

                                //ornament ofsett
                                if (command2[0] == '5')
                                {
                                    //put speed command
                                    notes.Add(cmdOrnamentOffset);
                                    //put new speed                                    
                                    string speedValue = command2.Substring(2, 2).Replace('.', '0');
                                    byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                    //put speed value
                                    notes.Add(speedByte);
                                }


                                //номер инструмента для включения                            
                                char samNumber_2 = QF1E_2[0];
                                if (samNumber_2 != '.')
                                {
                                    byte samByte = getByte0V(samNumber_2); //byte.Parse(samNumber_1.ToString(), System.Globalization.NumberStyles.HexNumber);
                                                                           //Console.WriteLine(samByte);
                                                                           //put start ornament command
                                    notes.Add(cmdSample);
                                    //number of ornament to apply
                                    notes.Add(samByte);
                                    //Console.WriteLine(orByte);
                                }


                                //instrument offset
                                if (command2[0] == '4')
                                {
                                    //put speed command
                                    notes.Add(cmdSampleOffset);
                                    //put new speed                                    
                                    string speedValue = command2.Substring(2, 2).Replace('.', '0');
                                    byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                    //put speed value
                                    notes.Add(speedByte);
                                }

                                //get one note
                                string note2 = ch2.Split(' ')[0];

                                if (notesDictionary.ContainsKey(note2))
                                {
                                    byte curNote = notesIndexes[note2];//get note index

                                    if (wasMute2)
                                    {
                                        wasMute2 = false;
                                        //add enable channel command
                                        curNote += 128;
                                    }
                                    notes.Add(curNote);
                                }
                                else
                                {
                                    switch (note2)
                                    {
                                        // R--
                                        case "R--":
                                            //disable channel
                                            notes.Add(cmdMuteCh);
                                            wasMute2 = true;

                                            break;
                                        // ---
                                        default:
                                            //keep playin
                                            notes.Add(cmdKeepPlayin);
                                            break;
                                    }

                                }
                            }
                            //=======================================================


                            //==============================================================

                            if (channels>=3)
                            {

                                //комманда скорости глобальная
                                string command3 = ch3.Split(' ')[2];

                                if (command3[0] == 'B')
                                {
                                    //put speed command
                                    notes.Add(cmdSpeed);
                                    //put new speed                                    
                                    string speedValue = command3.Substring(2, 2).Replace('.', '0');
                                    byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                    //put speed value
                                    notes.Add(speedByte);
                                }


                                //орнамент
                                string QF1E_3 = ch3.Split(' ')[1];
                                //номер орнамента для включения
                                char orNumber_3 = QF1E_3[2];
                                if (orNumber_3 != '.')
                                {
                                    byte orByte = byte.Parse(orNumber_3.ToString(), System.Globalization.NumberStyles.HexNumber);
                                    //put start ornament command
                                    notes.Add(cmdOrnament);
                                    //number of ornament to apply
                                    notes.Add(orByte);
                                    //Console.WriteLine(orByte);
                                }


                                //ornament ofsett
                                if (command3[0] == '5')
                                {
                                    //put speed command
                                    notes.Add(cmdOrnamentOffset);
                                    //put new speed                                    
                                    string speedValue = command3.Substring(2, 2).Replace('.', '0');
                                    byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                    //put speed value
                                    notes.Add(speedByte);
                                }


                                //номер инструмента для включения                            
                                char samNumber_3 = QF1E_3[0];
                                if (samNumber_3 != '.')
                                {
                                    byte samByte = getByte0V(samNumber_3); //byte.Parse(samNumber_1.ToString(), System.Globalization.NumberStyles.HexNumber);
                                                                           //Console.WriteLine(samByte);
                                                                           //put start ornament command
                                    notes.Add(cmdSample);
                                    //number of ornament to apply
                                    notes.Add(samByte);
                                    //Console.WriteLine(orByte);
                                }

                                //instrument offset
                                if (command3[0] == '4')
                                {
                                    //put speed command
                                    notes.Add(cmdSampleOffset);
                                    //put new speed                                    
                                    string speedValue = command3.Substring(2, 2).Replace('.', '0');
                                    byte speedByte = byte.Parse(speedValue, System.Globalization.NumberStyles.HexNumber);
                                    //put speed value
                                    notes.Add(speedByte);
                                }

                                //get one note
                                string note3 = ch3.Split(' ')[0];

                                if (notesDictionary.ContainsKey(note3))
                                {
                                    byte curNote = notesIndexes[note3];//get note index

                                    if (wasMute3)
                                    {
                                        wasMute3 = false;
                                        //add enable channel command
                                        curNote += 128;
                                    }
                                    notes.Add(curNote);
                                }
                                else
                                {
                                    switch (note3)
                                    {
                                        // R--
                                        case "R--":
                                            //disable channel
                                            notes.Add(cmdMuteCh);
                                            wasMute3 = true;

                                            break;
                                        // ---
                                        default:
                                            //keep playin
                                            notes.Add(cmdKeepPlayin);
                                            break;
                                    }

                                }
                            }
                            //=======================================================
                        }
                    }
                }


            }

            
             notes.Add(cmdPatChange);
            
            //disable channel
            //notes.Add(cmdMuteCh);
            //end song marker
            //notes.Add(cmdEndSong);

            //patterns.AddRange(notes);

            //File.WriteAllBytes("patterns.bin", patterns);
            //File.WriteAllBytes("order.bin", playOrder);
            //File.WriteAllBytes("notes.bin", notes.ToArray());
            //File.WriteAllBytes("ornaments.bin", ornaments.ToArray());
            //File.WriteAllBytes("samples.bin", samples.ToArray());

            //save freq table
            File.WriteAllBytes("freqTable.bin", freqTable);


            int patternsDelta = 0;
            int playOrderDelta = patternsDelta + patterns.Count();
            int ornamentsDelta = playOrderDelta + playOrder.Count();
            int samplesDelta = ornamentsDelta + ornaments.Count();
            int notesDelta = samplesDelta + samples.Count();


            header[0] = (byte)(patternsDelta % 256);
            header[1] = (byte)(patternsDelta / 256);

            header[2] = (byte)(playOrderDelta % 256);
            header[3] = (byte)(playOrderDelta / 256);

            header[4] = (byte)(ornamentsDelta % 256);
            header[5] = (byte)(ornamentsDelta / 256);

            header[6] = (byte)(samplesDelta % 256);
            header[7] = (byte)(samplesDelta / 256);

            header[8] = (byte)(notesDelta % 256);
            header[9] = (byte)(notesDelta / 256);


            //header
            File.WriteAllBytes(args[0].Replace(".vt2", ".vtk"), header);
            //save as vtk modeule            
            AppendAllBytes(args[0].Replace(".vt2", ".vtk"), patterns.ToArray());
            AppendAllBytes(args[0].Replace(".vt2", ".vtk"), playOrder.ToArray());
            AppendAllBytes(args[0].Replace(".vt2", ".vtk"), ornaments.ToArray());
            AppendAllBytes(args[0].Replace(".vt2", ".vtk"), samples.ToArray());
            AppendAllBytes(args[0].Replace(".vt2", ".vtk"), notes.ToArray());



        }
    }




}

