using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

using System.Collections;

using OpenCL.Net.Extensions;
using OpenCL.Net;


namespace OpenCL_GUI
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int count = 1000000;
        private int rangeTxt = 20;
        private int cores = 4000;
        private int inDataNum = 4;
        private int[] inData = new int[4];
        private int[] arrayResult;
        private int[] mapResult;
        private int[] _byteMapInt;
        private int[] byteMapInt = new int[256];

        string origFile = "D:\\Array_Original.txt";
        string encFile = "D:\\Array_Encoded.txt";
        string decFile = "D:\\Array_Decoded.txt";

        // Создаем устройство, контекст и очередь команд
        Event event0;
        ErrorCode err;
        Platform[] platforms;
        Device[] devices;
        Device device; //cl_device_id устройство;
        Context context;
        CommandQueue cmdQueue;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Создаем устройство, контекст и очередь команд
            platforms = Cl.GetPlatformIDs(out err);
            devices = Cl.GetDeviceIDs(platforms[0], DeviceType.Gpu, out err);
            device = devices[0]; //cl_device_id устройство;
            context = Cl.CreateContext(null, 1, devices, null, IntPtr.Zero, out err);
            cmdQueue = Cl.CreateCommandQueue(context, device, CommandQueueProperties.None, out err);
            inData[0] = count;
            statusTXT.Content = "Ожидание действий";
            coresAllTXT.Content = (int)KernelWorkGroupInfo.WorkGroupSize;            // Количество доступных ядер
            coresTXT.Content = cores.ToString();
            inData[1] = cores;
            _byteMapInt = new int[256 * cores];
            for (int i = 0; i < _byteMapInt.Length; i++) { _byteMapInt[i] = 0; }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Cоздаем и собираем программу из исходников OpenCL-C
            string programSource = Properties.Resources.kernelFunct1;
            Program program = Cl.CreateProgramWithSource(context, 1, new[] { programSource }, null, out err);
            Cl.BuildProgram(program, 0, null, string.Empty, null, IntPtr.Zero);  //"-cl-mad-enable"

            // Проверка на любые ошибки компиляции
            if (Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Status, out err).CastTo<BuildStatus>() != BuildStatus.Success)
            {
                if (err != ErrorCode.Success) errorTXT.Text = ("ERROR: " + "Cl.GetProgramBuildInfo" + " (" + err.ToString() + ")");
                errorTXT.Text = ("Cl.GetProgramBuildInfo != Success");
                errorTXT.Text = (Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, out err)).ToString();
            }

            // Создаем kernel-ы
            Kernel getRandom = Cl.CreateKernel(program, "getRandom", out err);  // создаем массив символов на GPU
            Kernel BuildMap = Cl.CreateKernel(program, "BuildMap", out err);    // заполняем карту байт GPU метод

            // Выделяем буферы ввода и заполняем данными
            Mem memInput = (Mem)Cl.CreateBuffer(context, MemFlags.ReadOnly, sizeof(int) * inDataNum, out err);

            // Выделяем память для вывода результатов
            Mem memOutput = (Mem)Cl.CreateBuffer(context, MemFlags.WriteOnly, sizeof(int) * count, out err);

            // копируем буфер хоста в буфер устройства
            Cl.EnqueueWriteBuffer(cmdQueue, (IMem)memInput, Bool.True, IntPtr.Zero, new IntPtr(sizeof(int) * inDataNum), inData, 0, null, out event0);

            // Получаем макисмальное количество вычислительных ядер
            IntPtr notused;
            InfoBuffer local = new InfoBuffer(new IntPtr(4));
            Cl.GetKernelWorkGroupInfo(getRandom, device, KernelWorkGroupInfo.WorkGroupSize, new IntPtr(sizeof(int)), local, out notused);

            // Задаем аргументы и очередь выполнения
            Cl.SetKernelArg(getRandom, 0, new IntPtr(4), memInput);
            Cl.SetKernelArg(getRandom, 1, new IntPtr(4), memOutput);
            Cl.SetKernelArg(getRandom, 2, new IntPtr(4), count);
            IntPtr[] workGroupSizePtr = new IntPtr[] { new IntPtr(cores) }; // Устанавливаем количество ядер для работы
            Cl.EnqueueNDRangeKernel(cmdQueue, getRandom, 1, null, workGroupSizePtr, null, 0, null, out event0);

            // Принудительно обработать очередь команд, дождитесь завершения всех команд
            Cl.Finish(cmdQueue);

            // Читаем результат
            arrayResult = new int[count];
            Cl.EnqueueReadBuffer(cmdQueue, (IMem)memOutput, Bool.True, IntPtr.Zero, new IntPtr(count * sizeof(int)), arrayResult, 0, null, out event0);

            Random randIn = new Random();
            int rand = randIn.Next(0, count - rangeTxt);

            outputTXT.Content = "";
            for (int i = 0; i < rangeTxt; i++)
            {
               outputTXT.Content += arrayResult[rand + i].ToString();
               if (i != 19) outputTXT.Content += ", ";
               if (i == 19) outputTXT.Content += "...";
            }
            System.IO.File.WriteAllText(origFile, string.Join("", arrayResult.Select(i => i.ToString()).ToArray()));

            outputLabelTXT.Content = "Случайный диапазон с " + rand.ToString() + " по " + (rand + rangeTxt).ToString();
            statusTXT.Content = "Создание массива савершено";

            string strings = String.Join("", arrayResult.Select(p => p.ToString()).ToArray());
            int[] bytesAsInts = Array.ConvertAll(Encoding.ASCII.GetBytes(strings), c => (int)c);            

            inData[3] = bytesAsInts.Length;
            Mem memInputData = (Mem)Cl.CreateBuffer(context, MemFlags.ReadOnly, sizeof(int) * inDataNum, out err);
            Mem memInputMap = (Mem)Cl.CreateBuffer(context, MemFlags.ReadOnly, sizeof(int) * bytesAsInts.Length, out err);
            Mem memOutputMap = (Mem)Cl.CreateBuffer(context, MemFlags.WriteOnly, sizeof(int) * _byteMapInt.Length, out err);
            Cl.EnqueueWriteBuffer(cmdQueue, 
                (IMem)memInputData, 
                Bool.True, 
                IntPtr.Zero, 
                new IntPtr(sizeof(int) * inDataNum), 
                inData, 
                0, 
                null, 
                out event0);

            Cl.EnqueueWriteBuffer(cmdQueue, 
                (IMem)memInputMap, 
                Bool.True, 
                IntPtr.Zero, 
                new IntPtr(sizeof(int) * bytesAsInts.Length),
                bytesAsInts, 
                0, 
                null, 
                out event0);

            // Задаем аргументы и очередь выполнения
            Cl.SetKernelArg(BuildMap, 0, new IntPtr(4), memInputData);
            Cl.SetKernelArg(BuildMap, 1, new IntPtr(4), memInputMap);
            Cl.SetKernelArg(BuildMap, 2, new IntPtr(4), memOutputMap);
            Cl.EnqueueNDRangeKernel(cmdQueue, BuildMap, 1, null, workGroupSizePtr, null, 0, null, out event0);

            // Принудительно обработать очередь команд, дождитесь завершения всех команд
            Cl.Finish(cmdQueue);

            // Читаем результат
            mapResult = new int[_byteMapInt.Length];
            Cl.EnqueueReadBuffer(cmdQueue, 
                (IMem)memOutputMap, 
                Bool.True, 
                IntPtr.Zero, 
                new IntPtr(_byteMapInt.Length * sizeof(int)),
                mapResult, 
                0, 
                null,
                out event0);
            
            for (int i = 0; i < mapResult.Length/cores; i++)
            {
                int temp = 0;
                for (int k = 0; k < cores; k++)
                {
                    temp += mapResult[i + 256*k];
                }
                
                byteMapInt[i] = temp;
            }

            //FillByteMap(bytesAsInts);                                  // заполняем карту байт CPU метод
            HuffmanTree.CreateHuffmanTree(byteMapInt);                   // строим дерево
            var huffmapMap = HuffmanTree.GetMap();                       // получаем карту кодировки
            CodeFile(origFile, encFile, huffmapMap, byteMapInt);         // кодируем
            //statusTXT.Content = "Файл зашифрован";
            Decode();
            //statusTXT.Content = "Файл расшифрован";
        }

        private static void CodeFile(string fileName, string newFileName, Dictionary<byte, BitArray> huffmapMap, IEnumerable<int> map)
        {   
            // Кодируем
            Byte myByte;
            var code = new List<BitArray>();
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                for (int i = 0; i < fs.Length; i++)
                {
                    myByte = (Byte)fs.ReadByte();
                    code.Add(huffmapMap[myByte]); //собираем закодированное сообщение
                }
            }
            var count = code.Sum(bitArray => bitArray.Count);


            var additionBits = (byte)((8 - count % 8) % 8); // количесто доп бит (ибо может получиться н байт с хвостиком, хвостик нужно будет добить нулями до байта)
            var bitCount = (count + 7) / 8; // количество байт в закодрованном файле

            var bitarr = new BitArray(count + additionBits, false);
            var add = 0;
            foreach (var t in code)
            {
                for (var j = 0; j < t.Count; j++)
                {
                    bitarr[add + j] = t[t.Count - 1 - j];
                }
                add += t.Count;//собираем из массива поток
            }
            var bytes = new byte[bitCount];
            bitarr.CopyTo(bytes, 0);

            using (var sw = new FileStream(newFileName, FileMode.Create))
            using (var bw = new BinaryWriter(sw))
            {
                foreach (var b in map)
                {
                    bw.Write(b);// сначала пишем в фаил карту баит
                }
                bw.Write(additionBits); // колличество доп бит
                foreach (var b in bytes)
                {
                    bw.Write(b); // и сам код
                }
            }
        }

        // Создание карты
        private void FillByteMap(int[] data)
        {
            for (int i = 0; i < data.Length; i++) {_byteMapInt[data[i]]++;}
        }

        private void Decode()
        {

            var map = new int[256];
            using (var stream = new FileStream(encFile, FileMode.Open))
            using (var binaryReader = new BinaryReader(stream))
            {
                for (int i = 0; i < map.Length; i++)
                {
                    map[i] = binaryReader.ReadInt32();
                }
                HuffmanTree.CreateHuffmanTree(map); // читаем карту байт из файла и по ней строим дерево
                int additionBits = binaryReader.ReadByte(); // читам сколько доп байт


                var bytes = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length - map.Length * 4 - 1);
                // читаем закод сообшение


                var bitArray = new BitArray(bytes);
                HuffmanTree currentNode = HuffmanTree.Root;


                using (var sw = new FileStream(decFile, FileMode.Create))
                using (var bw = new BinaryWriter(sw))
                {
                    for (int i = 0; i < bitArray.Length - additionBits; i++) // расшивровка
                    {
                        var boolean = bitArray[i]; // получам очередной бит
                        currentNode = boolean ? currentNode.RightBranch : currentNode.LeftBranch; // 1 вправо 0 - влево 
                        if (currentNode.LeftBranch == null) // нет детей - значит лист
                        {
                            bw.Write(currentNode.Value); // пишем значение листа в фаил
                            currentNode = HuffmanTree.Root; // переходим к корню
                        }
                    }
                }
            }
        }
    }
}
