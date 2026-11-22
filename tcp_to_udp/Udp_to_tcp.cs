using DirectShowLib;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using WinRT.Interop;
using System.Diagnostics;

using Encoder = System.Drawing.Imaging.Encoder;
using System.Reflection;
using Emgu.CV.Stitching;
using Windows.Media.Protection.PlayReady;

namespace tcp_to_udp
{
    class Udp_to_tcp
    {

        private static bool[] _isStreaming = new bool[3];
        // Камеры через DirectShow: кадры MJPG от камеры уходят в UDP как есть (CameraMjpg.cs)
        private static CameraMjpg[] _cameras = new CameraMjpg[3];

        TCPclient tcp_client_main;
        int device_numb = 0;
        int string_sec_remain = 0;
        int port_main = 6000;
        string ip_main = "192.168.1.200";

        long last_ms = 0;
        bool host_send = false;

        UdpClient udp_client1;
        IPEndPoint udp_addres_1;
        Thread udp_thread = null;

        UdpClient udp_client2;
        IPEndPoint udp_addres_2;
        Thread udp_thread_2 = null;

        Thread server_thread1 = null;
        TCPserver _TCPserver1 = null;

        Thread server_thread2 = null;
        TCPserver _TCPserver2 = null;

        Thread[] cams_thr = new Thread[3];

        long last_time_1 = DateTime.Now.Ticks;
        long last_time_2 = DateTime.Now.Ticks;

        long start_time = DateTime.Now.Ticks;

        bool initing1 = false;
        bool initing2 = false;


        volatile int[] ports_cam_orig = { 5000, 5001, 5002 };//bef, aft, pound
        volatile int[] ports_cam = { 5000, 5001, 5002 };


        //int[] ports_cam = { 5000, 5001, 5002 };

        List<string> coms1 = new List<string>();
        List<string> coms2 = new List<string>();

        List<Command> commands1 = new List<Command>();
        List<Command> commands2 = new List<Command>();
        long command_counter1 = 0;
        long command_counter2 = 0;



        

        


        StepperPrinter printer = new StepperPrinter();

        public void connect_udp_all()
        {
            //var settins_string = load_obj<SettingsString>("settings_printer.json");
            //ports_cam = settins_string.ports_cam;
            //ListAllCamerasButton_Click();
            //var set_test = new SettingsString();
            //set_test.ports_cam = ports_cam;
            //save_obj("settings_printer2.json", set_test);
            device_numb = 0;
            tcp_client_main = null;

            udp_client1 = null;
            GC.Collect();
            udp_client1 = new UdpClient(50000);
            string ip1 = "192.168.10.212";
            var port_udp1 = 52000;
            udp_addres_1 = new IPEndPoint(IPAddress.Parse(ip1), port_udp1);
            udp_client1.Connect(udp_addres_1);

            // Fabion: одна плата (Octopus, NUM_BOARD 1). Плата 2 не используется.
            udp_client2 = null;


            


            //commands1.Add(new Command(coms_str.Length + 1, "M588 A1"));

            /*for (int i = 0; i < 100; i++) commands1.Add(new Command( i, "M588 X"+i*5+" Y0 Z100 E100 W"+100*i));// J-" + ( Math.Round( i * 0.01,3)).ToString()));
            commands1.Add(new Command(100, "M588 A1"));
            commands1.Add(new Command(101, "M588 A1"));*/

            udp_thread = new Thread(recieve_udp_all);
            udp_thread.Start();



            _TCPserver1 = new TCPserver(62000);
            server_thread1 = new Thread(_TCPserver1.startServer);
            server_thread1.Start();

            /*var coms_str = StepperFrame.test();
            for (int i = 0; i < coms_str.Length; i++)
            {
                if(i==20) _TCPserver1.pushBuffer_in("M596 G1 A1 D0 C" + coms_str.Length + "\n");
                _TCPserver1.pushBuffer_in(coms_str[i] + '\n');
            }*/


            //tcp_client_main = new TCPclient();
            // Console.WriteLine("start con");
            // tcp_client_main.Connection(port_main, ip_main);

            //Process.Start("String_line4.exe");
            load_settings();


            Console.WriteLine("1 " + commands1.Count);

            //Console.WriteLine("start con done");
            // Fabion: камер нет
            //for (int i = 0; i < 3; i++) cams_thr[i] = start_cam(i, ports_cam[i]);
            while (true)
            {
                string? input = Console.ReadLine();
                if (input != null)
                {
                    _TCPserver1.pushBuffer_in(input +"\n");
                    //Console.WriteLine(input);
                }              
            } 
        }

        

        public static void show_delta_table(double[,,] table)
        {
            var im = new Image<Gray,byte>(table.GetLength(0), table.GetLength(1));
            for (int i = 0; i < im.Width; i++)
            {
                for (int j = 0; j < im.Height; j++)
                {
                    if(table[i, j, 0]!=0)
                    {
                        im.Data[i, j, 0] = 255;
                    }
                    
                }
            }
            CvInvoke.Imshow("table", im);
            CvInvoke.WaitKey();
        }


        

        

        


        

        

        


        public void load_commands(string[] coms)
        {
            foreach (var command in coms)
            {
                Console.WriteLine("command: " + command);
                if (command.Length > 3)
                {
                    if (command.Contains("num1"))
                    {
                        //Console.WriteLine("add com1: " + command);
                        var com_board = command.Replace("num1", "").Trim();
                        commands1.Add(new Command(command_counter1, com_board));
                        command_counter1++;
                    }
                    else if (command.Contains("num2"))
                    {
                        // Fabion: платы 2 нет — иначе очередь commands2 никогда не опустеет и шлюз встанет
                        Console.WriteLine("Плата 2 отсутствует, команда пропущена: " + command);
                    }
                    else if (command.Contains("main"))
                    {
                        exec_main_prog(command);
                    }
                }
            }
        }

        public void exec_main_prog(string command)
        {
            

            var com_board = command.Replace("main", "").Trim();
            if (command.Contains("M590") || command.Contains("M591"))
            {

                //Console.WriteLine("add com3: " + command);
                var command_af = com_board.Replace("  ", " ");
                command_af = command_af.Replace("  ", " ");
                var vars = command_af.Trim().Split(' ');

                if (vars.Length > 2)
                {
                    var ind_cam = Convert.ToInt32(vars[1]);
                    var val = Convert.ToInt32(vars[2]);
                    //Console.WriteLine(ind_cam + " " + val);
                    if (command.Contains("M590"))
                    {
                        if (ind_cam >= 0 && ind_cam < _cameras.Length && _cameras[ind_cam] != null)
                            _cameras[ind_cam].SetExposure(val);
                    }
                    else if (command.Contains("M591"))
                    {
                        ports_cam[ind_cam] = val;
                    }
                }
            }
            else if (command.Contains("M593"))
            {

                tcp_client_main.Connection(port_main, ip_main);
            }

            else if (command.Contains("M594"))
            {
                var val = val_from_command(com_board);

            }
            else if (command.Contains("M595"))
            {
                var val = val_from_command(com_board);

            }

            else if (command.Contains("M596")) // prog load
            {
                var com_re = com_board.Replace("M596 ", "").Trim();
                printer.prog_orig_commands.Add(com_re);

            }

            else if (command.Contains("M597"))// prog control
            {
                var val = val_from_command(com_board);
                Console.WriteLine("M597 val: " + val);
                if (val == 0)
                {
                    var frames_xyz_list = printer.convert_g_code_to_stepperframes(printer.prog_orig_commands.ToArray(), printer).ToList();
                    if (frames_xyz_list != null)
                    {
                        printer.start_main_alternate_prog(frames_xyz_list.ToArray());
                    }


                }
                else if (val == 1)
                {
                    printer.prog_state = StepperPrinter.programm_state.PAUSE;
                }
                else
                {
                    //load_commands(new string[] { "num1 M588 A0" });
                    load_commands(new string[] { "num1 M589 S" });
                    printer.prog_state = StepperPrinter.programm_state.STOP;
                }
            }
            else if (command.Contains("M598")) // prog clear
            {
                printer.prog_commands = new List<StepperFrame>();
                printer.prog_orig_commands = new List<string>();
            }
            else if (command.Contains("M610"))//set jog vel
            {
                var val = val_from_command_d(com_board);
                printer.jog_xyz_vel = val;
            }

            else if (command.Contains("M611") && printer.all_steps_kinem == 0)//jog 
            {

                var val = val_from_command(com_board);
                var jog_orig = new List<StepperFrame>();
                var fr_cur = new StepperFrame(new Point3d_GL(0, 0, 0), 0, 0);
                fr_cur.vel = printer.jog_xyz_vel;
                jog_orig.Add(fr_cur);
                var fr_jog = fr_cur.clone();
                fr_jog.p_xyz = fr_jog.p_xyz.add_mask(val, printer.jog_len);
                jog_orig.Add(fr_jog);

                printer.cur_coordinate_system = StepperPrinter.coordinate_system.CURRENT;
                //offset_frame.p_xyz = new Point3d_GL(0, 0, 0);
                printer.start_main_alternate_prog(jog_orig.ToArray());


            }
            else if (command.Contains("M612"))//set_zero
            {
                var val = vals_from_command_d(com_board,8);
                //printer.offset_zero_pos[printer.tool_active] = 

                var cur_fr = printer.cur_frame.clone();
                var x = val[0];
                var y = val[1];
                var z = val[2];
                var e = val[3];

                var x_en = val[4];
                var y_en = val[5];
                var z_en = val[6];
                var e_en = val[7];

                if (x_en == 1) printer.offset_zero_pos[printer.tool_active].p_xyz.x = cur_fr.p_xyz.x - x; 
                if (y_en == 1) printer.offset_zero_pos[printer.tool_active].p_xyz.y = cur_fr.p_xyz.y - y;
                if (z_en == 1) printer.offset_zero_pos[printer.tool_active].p_xyz.z = cur_fr.p_xyz.z - z;

                printer.tool_calibrated_manual[printer.tool_active] = true;

            }
            else if (command.Contains("M614"))//settings load
            {

                load_settings();
            }
            else if (command.Contains("M615"))//move zero p
            {

                var frames_xyz_list = new StepperFrame[]
                {
                                        new StepperFrame(printer.cur_frame.p_xyz-printer.offset_frame.p_xyz,0,printer.jog_xyz_vel),
                                        new StepperFrame(new Point3d_GL(),0,printer.jog_xyz_vel),
                };
                printer.start_main_alternate_prog(frames_xyz_list.ToArray());

            }
            else if (command.Contains("M616"))//remember_p 
            {
                var val = val_from_command(com_board);
                if (val >= 0 && val < printer.bed_calib_ps.Length)
                {
                    printer.bed_calib_ps[val] = printer.cur_frame;
                }

                if (val < 0)
                {
                    printer.bed_calib_vec = new Point3d_GL(0, 0, 1);
                }

                if (val > printer.bed_calib_ps.Length)
                {

                    var p1 = printer.bed_calib_ps[0].p_xyz;
                    var p2 = printer.bed_calib_ps[1].p_xyz;
                    var p3 = printer.bed_calib_ps[2].p_xyz;
                    var vecn = new Flat3d_GL(p1, p2, p3).n;
                    if (Math.Abs(vecn.z) > 0.5)
                    {
                        if (vecn.z < 0)
                        {
                            vecn.x *= -1;
                            vecn.y *= -1;
                            vecn.z *= -1;
                        }
                        printer.bed_calib_vec = new Point3d_GL(vecn.x, vecn.y, vecn.z);
                        Console.WriteLine("printer.bed_calib_vec: " + printer.bed_calib_vec);
                    }
                    else
                    {
                        Console.WriteLine("vecn.z < 0.5");
                    }

                }
            }

            else if (command.Contains("M617"))//set jog vel
            {
                var val = val_from_command(com_board);
                printer.koef_extrus = val / 100d;
            }

            else if (command.Contains("M618"))//set jog vel
            {
                var val = val_from_command(com_board);
                printer.koef_vel = val / 100d;
            }


            //---------------- Fabion: шприцы ----------------
            // M620 n — сделать активным шприц n (0..2): поднять Z текущего шприца в парковку,
            //          дальше программы ведут Z и E шприца n.
            // M621   — поднять в парковку все три Z.
            // M622 n — поднять в парковку Z шприца n.
            else if (command.Contains("M620"))
            {
                var tool_dest = val_from_command(com_board);
                if (tool_dest < 0 || tool_dest >= StepperPrinter.TOOLS_COUNT)
                {
                    Console.WriteLine("M620: нет шприца " + tool_dest);
                    return;
                }
                var prog_cur = new List<StepperFrame>();
                if (tool_dest != printer.tool_active)
                    prog_cur.Add(park_z_frame(printer.tool_active, true));
                printer.tool_active = tool_dest;
                printer.set_all_pos(printer.all_pos);
                printer.cur_frame = printer.solve_fk(printer.cur_pos);
                printer.cur_frame.p_xyz.z -= printer.comp_off_bed(printer.cur_frame.p_xyz);
                prog_cur.Add(new StepperFrame(1, 588, "K" + printer.z_motor() + " L" + printer.e_motor(), false));
                printer.cur_coordinate_system = printer.dest_coordinate_system;
                printer.offset_frame = printer.comp_offset(printer.cur_coordinate_system);
                printer.start_alternate_prog(prog_cur.ToArray());
                Console.WriteLine("Активный шприц: " + (tool_dest + 1));
            }
            else if (command.Contains("M621"))
            {
                var prog_cur = new List<StepperFrame>();
                for (int t = 0; t < StepperPrinter.TOOLS_COUNT; t++)
                    prog_cur.Add(park_z_frame(t, t == StepperPrinter.TOOLS_COUNT - 1));
                printer.start_alternate_prog(prog_cur.ToArray());
            }
            else if (command.Contains("M622"))
            {
                var t = val_from_command(com_board);
                if (t >= 0 && t < StepperPrinter.TOOLS_COUNT)
                    printer.start_alternate_prog(new StepperFrame[] { park_z_frame(t, true) });
            }
            else if (command.Contains("M631"))//set xy_calibrate
            {
                if (!printer.tool_calibrated_auto[printer.tool_active])
                {
                    printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL;
                    //printer.offset_frame.p_xyz = new Point3d_GL(0, 0, 0);
                    double save_dist_nossle = 10;
                    printer.calibrate_nossle_stage_counter = 0;

                    printer.calibrating_nossle = true;
                    var prog_cur = new List<StepperFrame>();
                    var cur_fr_dest = printer.cur_frame.clone();
                    cur_fr_dest.vel = printer.jog_xyz_vel;
                    prog_cur.Add(cur_fr_dest.clone());
                    if (cur_fr_dest.p_xyz.z < printer.settins_string.calibrate_nossle_z[0] + save_dist_nossle)
                    {
                        cur_fr_dest.p_xyz.z += save_dist_nossle;
                    }
                    //add offset for different nossle    [cur_nossle]
                    prog_cur.Add(cur_fr_dest.clone());
                    prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0] + save_dist_nossle), 0, printer.jog_xyz_vel));
                    prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.jog_xyz_vel));
                    printer.start_alternate_prog(prog_cur.ToArray());
                    printer.calibrate_nossle_stage_counter = 1;
                    printer.calibrate_nossle_frames = new List<StepperFrame>();
                    Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                }
            }
            else if (command.Contains("M632"))
            {
                var val = val_from_command(com_board);
                

            }


            else if (command.Contains("M636"))
            {
                var val = val_from_command(com_board);
                printer.cur_nossle_type = val;

            }

            else if (command.Contains("M637"))
            {
                var val = val_from_command(com_board);
                printer.cur_plate_type = val;

            }


            else if (command.Contains("M638"))
            {
                var val = vals_from_command_d(com_board, 3);
                printer.offset_nossle[printer.tool_active].p_xyz = new Point3d_GL(val[0], val[1], val[2]);

            }
            else if (command.Contains("M639"))
            {
                // активный шприц без движения (например, после ручной смены)
                var val = val_from_command(com_board);
                if (val >= 0 && val < StepperPrinter.TOOLS_COUNT)
                {
                    printer.tool_active = val;
                    printer.set_all_pos(printer.all_pos);
                    load_commands(new string[] { "num1 M588 K" + printer.z_motor() + " L" + printer.e_motor() });
                }
            }
            else if (command.Contains("M640"))
            {
                var val = val_from_command(com_board);
                switch(val)
                {
                    case 0: printer.cur_coordinate_system = StepperPrinter.coordinate_system.MANUAL; break;
                    case 1: printer.cur_coordinate_system = StepperPrinter.coordinate_system.AUTO; break;
                    case 2: printer.cur_coordinate_system = StepperPrinter.coordinate_system.CURRENT; break;
                    case 3: printer.cur_coordinate_system = StepperPrinter.coordinate_system.INTERNAL; break;
                }
            }
            else if (command.Contains("M641"))
            {
                var val = val_from_command(com_board);
                switch (val)
                {
                    case 0: printer.dest_coordinate_system = StepperPrinter.coordinate_system.MANUAL; printer.cur_coordinate_system = StepperPrinter.coordinate_system.MANUAL; Console.WriteLine(printer.cur_coordinate_system); break;
                    case 1: printer.dest_coordinate_system = StepperPrinter.coordinate_system.AUTO; printer.cur_coordinate_system = StepperPrinter.coordinate_system.AUTO; break;
                }
            }
            else if (command.Contains("M642"))
            {
                var val = val_from_command(com_board);
                switch (val)
                {
                    case 0: printer.table_out = false; break;
                    case 1: printer.table_out = true; break;
                }
            }
            else if (command.Contains("M643"))
            {
                var val = val_from_command(com_board);
                switch (val)
                {
                    case 0: printer.cover_up = false; break;
                    case 1: printer.cover_up = true; break;
                }
            }




            else if (command.Contains("M700"))//set all stop
            {
                printer.prog_state =  StepperPrinter.programm_state.STOP;
                _TCPserver1.pushBuffer_in("num1 M589 S" + "\n");
                printer.calibrating_nossle = false;
                printer.main_alternately_commands_counter = printer.main_alternately_commands.Count + 1;
            }

            else if (command.Contains("M701"))//stop condition
            {
                var vals = vals_from_command(com_board, 6);

                printer.consider_wait_all_steps_kinem = vals[0];
                printer.consider_all_steps1 = vals[1];
                printer.consider_all_steps2 = vals[2];
                printer.consider_prog_done = vals[3];
                printer.consider_wait_en = vals[4];
                printer.consider_wait_term = vals[5];


            }
        }

        void recieve_udp_all()
        {
            int count_ins = 0;

            long count_send1 = 0;
            long count_send2 = 0;
            bool err_con_tcp = false;




            // Fabion: картезианская кинематика, таблица дельты не нужна
            printer.bed_calib_vec = printer.bed_calib_vec.normalize();

            /*var prog_orig_commands = new List<string>()
            {
                "G92 X0 Y0 Z0 E0",
                "G1 X0 Y0 F600",
                "G1 X10 E0.1",
                "G1 X10 Y10 E0.2",
                "G1 X0 Y10 E0.3",
                "G1 X0 Y0 E0.4 F600",
                "G91",
                "G1 Z-10",
                "G90",
                "G92 X0 Y0 Z0 E0",
                "G1 X0 Y0 F600",
                "G1 X10 E0.1",
                "G1 X10 Y10 E0.2",
                "G1 X0 Y10 E0.3",
                "G1 X0 Y0 E0.4 F600",
            };

            var frames_xyz_test_2 = StepperFrame.convert_g_code_to_stepperframes(prog_orig_commands.ToArray(), printer);*/
            //prog_commands = StepperFrame.convert_g_code(frames_xyz_test_2, ref printer, offset_frame).ToList();

            /*prog_orig_commands = new List<string>()
            {
                "G1 X0 Y0 F600",
                "G1  X10",
                "G1 X10 Y10",
                "G1  X0 Y10 E1",
                "G1X0 Y0 Z0 E1",
                "G1 X0 Y0 F600",
                "G1  X10",
                "G1 X10 Y10",
                "G1  X0 Y10 E0",
                "G1X0 Y0 Z0 E1",

            };

            var frames_xyz_test_2 = StepperFrame.convert_g_code_to_stepperframes(prog_orig_commands.ToArray(), printer);
            prog_commands = StepperFrame.convert_g_code(frames_xyz_test_2, printer, offset_frame).ToList();*/
            //printer.delta_comp_test();

            //show_delta_table(printer.delta_fk_table_ps);
            //var p_abc_ik = printer.delta_ik(new Point3d_GL(0, 0, -174.72));
            //Console.WriteLine("ik: "+p_abc_ik);
            // printer.solve_fk(new long[] { 1000, 1000, 1000 });

            while (udp_client1 != null)
            {
                // Console.WriteLine("recive udp");
                int com_num = 0;
                bool parsed_val = false;

                //Console.Clear();
                //coms2 = new List<string>();

                if (_TCPserver1.connected)
                {
                    err_con_tcp = true;
                    //data =;
                    if (_TCPserver1.getBufferLen() > 3)
                    {
                        var data = _TCPserver1.getBuffer();
                        //Console.WriteLine("data bef: " + data);
                        data = data.Replace('\r', ' ');
                        var coms = data.Trim().Split('\n');
                        //Console.WriteLine("data: " + data);
                        load_commands(coms);

                    }
                }
                else
                {
                    if (err_con_tcp)
                    {
                        Console.WriteLine("not connected interface");
                        err_con_tcp = false;
                    }

                }

                if (udp_client1.Available > 0)
                {
                    //Console.WriteLine("udp_client1.Available > 0: " );
                    var res = udp_client1.Receive(ref udp_addres_1);

                    long dtime = DateTime.Now.Ticks - last_time_1;
                    last_time_1 = DateTime.Now.Ticks;

                    var cur_time_ms = DateTime.Now.Millisecond;

                    var dtime_ms = cur_time_ms - last_ms;

                    if (DateTime.Now.Millisecond > 500)
                    {
                        if (!host_send)
                        {
                            //Console.WriteLine(dtime_ms);
                            //var mes_serv = device_numb + "" + string_is_ending + "" + pound_is_ending;
                            //Console.WriteLine("send server " + mes_serv);
                            // tcp_client_main.send_mes(mes_serv);
                            host_send = true;
                        }
                    }
                    else
                    {
                        host_send = false;
                    }

                    if (dtime > 5000000)
                    {
                        initing1 = false;
                        Console.WriteLine("init 1 re:" + dtime + " " + (last_time_1 - start_time));
                    }


                    //Console.WriteLine(DateTime.Now.Ticks);
                    var mes = Encoding.ASCII.GetString(res);
                    //Console.WriteLine("res1: " + mes);
                    if (res != null)
                    {
                        if (_TCPserver1.connected)
                        {
                            //_TCPserver1.send_mes(mes);
                            var frame_out = printer.cur_frame.p_xyz.Clone();
                            //frame_out.z -= printer.comp_off_bed(frame_out);
                            
                            // Fabion: последняя цифра — активный шприц (0..2), остальные не используются
                            var tools = "0000" + printer.tool_active;

                            var offs_intern = printer.comp_offset(StepperPrinter.coordinate_system.INTERNAL).p_xyz;

                            //Console.WriteLine(offs_intern);

                            var frame_out_cur = frame_out.Clone() - printer.offset_frame.p_xyz;
                            var frame_out_mash = frame_out.Clone() - offs_intern;
                            var frame_out_tool = frame_out.Clone() - printer.comp_offset(printer.dest_coordinate_system).p_xyz;

                            _TCPserver1.pushBuffer(mes + " " + tools + " " + frame_out_cur.ToString() + " " + frame_out_mash.ToString() + " " + frame_out_tool.ToString() +" "+printer.cur_coordinate_system+
                                // Fabion: машинные координаты Z1 Z2 Z3 E1 E2 E3 (мм)
                                " " + fmt_mm(printer.motor_mm(2)) + " " + fmt_mm(printer.motor_mm(3)) + " " + fmt_mm(printer.motor_mm(4)) +
                                " " + fmt_mm(printer.motor_mm(5)) + " " + fmt_mm(printer.motor_mm(6)) + " " + fmt_mm(printer.motor_mm(7)) + "\n");
                        }
                        //Console.WriteLine(mes);
                        // Console.WriteLine("len1: " + coms1.Count);
                        var vars_from_mes = mes.Split(' ');
                        var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);

                        //Console.WriteLine(vars_from_mes.Length);
                        if (vars_from_mes.Length >= 9)
                        {
                            //try
                            {
                                var ring_counter = (long)Convert.ToInt32(vars_from_mes[2]);
                                var cur_send = (long)Convert.ToInt32(vars_from_mes[3]);


                                if (vars_from_mes[4].Length == 5)
                                {
                                    printer.all_steps1 = Convert.ToInt32(vars_from_mes[4][0]) - 48;
                                    printer.all_steps_kinem = Convert.ToInt32(vars_from_mes[4][1]) - 48;
                                    printer.ring_buf_en = Convert.ToInt32(vars_from_mes[4][2]) - 48;
                                    printer.wait_en = Convert.ToInt32(vars_from_mes[4][3]) - 48;
                                    printer.wait_term = Convert.ToInt32(vars_from_mes[4][4]) - 48;   // Fabion: нагрев на этой же плате

                                    //Console.WriteLine("1:" + vars_from_mes[4]);
                                }

                                if (cur_send == 0)
                                {

                                    //ring_en = Convert.ToInt32(vars_from_mes[6]);

                                    

                                    //ring_buf_en = Convert.ToInt32(vars_from_mes[6]);


                                    var nossle_calib_vals = vars_from_mes[10];
                                    printer.calibr_x = Convert.ToInt32(nossle_calib_vals[0]) - 48;
                                    printer.calibr_y = Convert.ToInt32(nossle_calib_vals[1]) - 48;
                                    printer.calibr_z = Convert.ToInt32(nossle_calib_vals[2]) - 48;

                                    // Fabion: датчиков головок нет
                                }
                                var cur_prog_line_board = Convert.ToInt64(vars_from_mes[2]);
                                //cur position-----------------------------------------------------------------
                                if (cur_send == 1)
                                {
                                    var cur_poses = new long[8];
                                    for (int i = 0; i < 8; i++)
                                    {
                                        cur_poses[i] = Convert.ToInt64(vars_from_mes[5 + i]);
                                    }


                                    // Fabion: X, Y, Z и E активного шприца
                                    printer.set_all_pos(cur_poses);

                                    printer.cur_frame = printer.solve_fk(printer.cur_pos);



                                    //Console.WriteLine(cur_frame.e);
                                    printer.cur_frame.p_xyz.z -= printer.comp_off_bed(printer.cur_frame.p_xyz);
                                    /**/
                                }


                                var commands_load = (commands1.Count == 0 && commands2.Count == 0 && _TCPserver1.buffer_out.Length == 0);
                                var printer_work =
                                    (printer.consider_all_steps1 == 1 && printer.all_steps1 == 1) ||
                                    (printer.consider_all_steps2 == 1 && printer.all_steps2 == 1) ||
                                    (printer.consider_wait_all_steps_kinem == 1 && printer.all_steps_kinem == 1) ||
                                    (printer.consider_prog_done == 1 && printer.ring_buf_en == 1) ||
                                    (printer.consider_wait_en == 1 && printer.wait_en == 1) ||
                                    (printer.consider_wait_term == 1 && printer.wait_term == 1);


                                //prog_work-----------------------------------------------------------------
                                /*if (prog_state == programm_state.MOVE && (cur_prog_line + safe_len_send_val < cur_prog_line_board || cur_prog_line < lookup_buf - safe_len_send_val))
                                {
                                    //Console.WriteLine(mes);
                                    if (cur_prog_line < prog_commands?.Count)
                                    {
                                        _TCPserver1.pushBuffer_in(prog_commands[cur_prog_line].get_command(printer) + "\n");
                                        cur_prog_line++;



                                    }
                                }

                                //jog work-----------------------------------------------------------------
                                if ((prog_state == programm_state.JOG && (cur_jog_line + safe_len_send_val < cur_prog_line_board || cur_jog_line < lookup_buf - safe_len_send_val))&& commands_load)
                                {
                                    if (cur_jog_line < jog_commands?.Count)
                                    {

                                        var com = jog_commands[cur_jog_line].get_command(printer);
                                        load_commands(new string[] { com });
                                        //Console.WriteLine("cur_prog_line_board: " + cur_prog_line_board + "; cur_jog_line: " + cur_jog_line+"/"+ jog_commands?.Count+"; "+com);

                                        cur_jog_line++;
                                    }
                                }*/

                                //alternately work-----------------------------------------------------------------
                                if (printer.prog_state ==  StepperPrinter.programm_state.ALTERNATELY)
                                {
                                    
                                    //Console.WriteLine("cur: "+all_steps_kinem + " " + all_steps1 + " " + all_steps2 + " " + ring_buf_en + " " + wait_en + " " + wait_term);
                                    //Console.WriteLine("con: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);
                                    if (commands_load && !printer_work)
                                    {
                                        if (printer.cur_alternately_line < printer.alternately_commands.Count)
                                        {

                                            var cur_alternately_line_internal = printer.alternately_commands[printer.cur_alternately_line].line_number;
                                            var printer_ready_kinem = ((cur_alternately_line_internal + printer.safe_len_send_val < cur_prog_line_board || cur_alternately_line_internal < printer.lookup_buf - printer.safe_len_send_val));// && cur_alternately_line_internal < stop_len

                                            if (printer_ready_kinem || !printer.alternately_commands[printer.cur_alternately_line].kinematic)
                                            {
                                                if (printer.alternately_commands[printer.cur_alternately_line].reset_extr) printer.abs_pos_extr = 0;
                                                var com = printer.alternately_commands[printer.cur_alternately_line].get_command(printer);

                                                //Console.WriteLine("alt: "+cur_alternately_line+"/" + alternately_commands.Count + " " + com);
                                                //Console.WriteLine("cur: "+all_steps_kinem + " " + all_steps1 + " " + all_steps2 + " " + ring_buf_en + " " + wait_en + " " + wait_term);
                                                //Console.WriteLine("con: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);

                                               
                                                load_commands(new string[] { com });


                                                printer.consider_all_steps1 = printer.alternately_commands[printer.cur_alternately_line].consider_all_steps1;
                                                printer.consider_all_steps2 = printer.alternately_commands[printer.cur_alternately_line].consider_all_steps2;
                                                printer.consider_wait_all_steps_kinem = printer.alternately_commands[printer.cur_alternately_line].consider_wait_all_steps_kinem;
                                                printer.consider_wait_en = printer.alternately_commands[printer.cur_alternately_line].consider_wait_en;
                                                printer.consider_wait_term = printer.alternately_commands[printer.cur_alternately_line].consider_wait_term;
                                                printer.consider_prog_done = printer.alternately_commands[printer.cur_alternately_line].consider_prog_done;
                                                //Console.WriteLine("set: " + consider_wait_all_steps_kinem + " " + consider_all_steps1 + " " + consider_all_steps2 + " " + consider_prog_done + " " + consider_wait_en + " " + consider_wait_term);
                                                printer.cur_alternately_line++;

                                                if(printer.consider_prog_done == 1)
                                                {
                                                    //Console.WriteLine("set onsider_prog_done==1");
                                                }
                                                else
                                                {
                                                   // Console.WriteLine("set onsider_prog_done==0");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            /*Console.WriteLine("consider_prog_done == 1 && ring_buf_en == 1 " + (consider_prog_done == 1 && ring_buf_en == 1));
                                            Console.WriteLine("printer_work: " + printer_work);
                                            Console.WriteLine("STOP; ring_buf_en:" + ring_buf_en+" "+ alternately_commands[cur_alternately_line-1].consider_prog_done+" "+(cur_alternately_line - 1));*/
                                            printer.prog_state = StepperPrinter.programm_state.STOP;
                                        }
                                    }
                                        
                                    

                                }

                                //main alternately work-----------------------------------------------------------------



                                //------
                                if (printer.main_alternately_commands_exec && printer.prog_state == StepperPrinter.programm_state.STOP && commands_load && !printer_work && printer.main_alternately_commands_counter < printer.main_alternately_commands.Count && !printer.calibrating_nossle)
                                {
                                    {
                                        if (printer.main_alternately_commands[printer.main_alternately_commands_counter][0].plate_num == 0)
                                        {

                                            var com = printer.main_alternately_commands[printer.main_alternately_commands_counter][0].get_command(printer);
                                            Console.WriteLine("main:"+ printer.main_alternately_commands_counter + " " + printer.main_alternately_commands.Count + " " + com);
                                            exec_main_prog(com);
                                        }
                                        else
                                        {
                                            printer.cur_frame.e = printer.abs_pos_extr;
                                            printer.start_alternate_prog(printer.main_alternately_commands[printer.main_alternately_commands_counter].ToArray());
                                        }

                                        printer.main_alternately_commands_counter++;
                                    }

                                }

                                //delta calib_handler-----------------------------------------------------------------

                                // Fabion: калибровки дельты нет (M613 убрана), delta_calibr_en всегда false
                                if (printer.delta_calibr_en)
                                {
                                var cur_delta_calib = Convert.ToInt32(vars_from_mes[8]);
                                var cur_ring_buf_en = Convert.ToInt32(vars_from_mes[9]);
                                var cur_homing = Convert.ToInt32(vars_from_mes[10]);



                                    if (printer.prev_homing - cur_homing == 1)
                                    {
                                        _TCPserver1.pushBuffer_in("num1 M589 Y80" + "\n");
                                        printer.delta_calibr_counter = 0;

                                        Console.WriteLine("prev_homing - cur_homing == 1");
                                    }

                                    if (printer.prev_delta_calib - cur_delta_calib == 1 && !printer.delta_calib_go_next_p)
                                    {
                                        Console.WriteLine("cur_delta_calib - prev_delta_calib == 1");
                                        if (printer.delta_calibr_counter == 0)
                                        {
                                            printer.offset_frame = printer.cur_frame;
                                        }


                                        printer.points_res_calibrate_abc[printer.delta_calibr_counter] = (long[])printer.cur_pos.Clone();
                                        printer.delta_calibr_counter++;
                                        Console.WriteLine("calibr_counter: " + printer.delta_calibr_counter + "/ " + printer.delta_calibr_counter_max);
                                        if (printer.delta_calibr_counter >= printer.delta_calibr_counter_max)
                                        {
                                            printer.delta_calibr_en = false;
                                            printer.delta_comp_prop_calibr();
                                            _TCPserver1.pushBuffer_in("num1 M589 X180" + "\n");
                                        }
                                        var delta_orig_commands = new List<string>();
                                        delta_orig_commands.Add("G1 X" + printer.cur_frame.p_xyz.x + " Y" + printer.cur_frame.p_xyz.y + " Z" + printer.cur_frame.p_xyz.z + " F1200");
                                        //delta_orig_commands.Add("G1 Z"+2*printer.points_for_calibrate_xy[printer.delta_calibr_counter].z);
                                        delta_orig_commands.Add("G1 Z" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].z);
                                        delta_orig_commands.Add("G1 X" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].x + " Y" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].y);
                                        //prog_orig_commands g code to next point
                                        var frames_xyz_test = printer.convert_g_code_to_stepperframes(delta_orig_commands.ToArray(), printer);



                                        /*prog_commands = StepperFrame.convert_g_code(frames_xyz_test, ref printer, offset_frame).ToList();
                                        if (prog_commands != null)
                                        {
                                            prog_commands = StepperFrame.prepare_g_code_to_load(prog_commands.ToArray(), ref printer).ToList();
                                            max_count_cur_prog = prog_commands.Count;
                                            cur_prog_line = 0;
                                            prog_state = programm_state.MOVE;

                                            delta_calib_go_next_p = true;
                                            Console.WriteLine(delta_calib_go_next_p + " " + cur_ring_buf_en + " " + cur_prog_line + " ");
                                        }*/

                                    }

                                    /*if (prev_delta_calib - cur_delta_calib == 1 && !delta_calib_go_next_p)
                                    {
                                        var delta_orig_commands = new List<string>();
                                        delta_orig_commands.Add("G1 Z" + printer.points_for_calibrate_xy[printer.delta_calibr_counter].z + " F600");
                                        //prog_orig_commands g code to next point
                                        var frames_xyz = StepperFrame.convert_g_code_to_stepperframes(delta_orig_commands.ToArray(), printer);
                                        max_count_cur_prog = frames_xyz.Length;
                                        prog_commands = StepperFrame.convert_g_code(frames_xyz, printer, offset_frame).ToList();
                                        cur_prog_line = 0;
                                        prog_state = programm_state.MOVE;
                                        delta_calib_nozzle_up = false;

                                    }
                                    */
                                    if (printer.delta_calib_go_next_p && cur_ring_buf_en == 0) Console.WriteLine(printer.cur_prog_line + "/" + printer.max_count_cur_prog);


                                    if (printer.delta_calib_go_next_p && cur_ring_buf_en == 0 && printer.cur_prog_line > printer.max_count_cur_prog - 5)
                                    {
                                        Console.WriteLine(printer.delta_calib_go_next_p + " " + cur_ring_buf_en + " " + printer.cur_prog_line + " " + printer.max_count_cur_prog / 2);
                                        Console.WriteLine("delta_calib_go_next_p && cur_ring_buf_en == 0");
                                        _TCPserver1.pushBuffer_in("num1 M589 Y80" + "\n");
                                        printer.delta_calib_go_next_p = false;
                                    }

                                }

                                // (prev_delta_calib / prev_homing — только для дельты, не нужны)
                                // Console.WriteLine(mes);
                                printer.prev_pos = (long[])printer.cur_pos.Clone();


                                //nossle calib_handler-----------------------------------------------------------------
                                if (printer.calibrating_nossle)
                                {
                                    //var commands_load = (commands1.Count == 0 && commands2.Count == 0 && _TCPserver1.buffer_out.Length == 0 && ring_buf_en == 0);
                                    //var printer_work = (all_steps1 == 1 || all_steps2 == 1);

                                    if(commands_load && !printer_work)
                                    { //----CALIBR_X------------------------------------------------------------------------------------
                                        if (printer.calibrate_nossle_stage_counter == 1  && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[1], printer.settins_string.calibrate_nossle_y[1], printer.settins_string.calibrate_nossle_z[1]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 2;

                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);

                                        }

                                        if (printer.calibrate_nossle_stage_counter == 2 && printer.calibr_x == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[1], printer.settins_string.calibrate_nossle_y[1], printer.settins_string.calibrate_nossle_z[1]), 0, printer.jog_xyz_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 3;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }


                                        if (printer.calibrate_nossle_stage_counter == 3 && printer.prog_state == StepperPrinter.programm_state.STOP)// &&prog_state == programm_state.STOP && prog_done == 1
                                        {
                                            //calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 4;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }

                                        if (printer.calibrate_nossle_stage_counter == 4 && printer.calibr_x == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "A0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.jog_xyz_vel));
                                            //prog_cur.Add(new StepperFrame((calibrate_nossle_frames[0].p_xyz + calibrate_nossle_frames[1].p_xyz)/2, 0, calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 5;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }



                                        //----CALIBR_Y------------------------------------------------------------------------------------

                                        if (printer.calibrate_nossle_stage_counter == 5 && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[2], printer.settins_string.calibrate_nossle_y[2], printer.settins_string.calibrate_nossle_z[2]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 6;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);

                                        }

                                        if (printer.calibrate_nossle_stage_counter == 6 && printer.calibr_y == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[2], printer.settins_string.calibrate_nossle_y[2], printer.settins_string.calibrate_nossle_z[2]), 0, printer.jog_xyz_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 7;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }


                                        if (printer.calibrate_nossle_stage_counter == 7 && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            //calibrate_nossle_frames.Add(cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(new Point3d_GL(printer.settins_string.calibrate_nossle_x[0], printer.settins_string.calibrate_nossle_y[0], printer.settins_string.calibrate_nossle_z[0]), 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 8;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }

                                        if (printer.calibrate_nossle_stage_counter == 8 && printer.calibr_y == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "B0", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            var fr_z_cal = new StepperFrame((printer.calibrate_nossle_frames[2].p_xyz + printer.calibrate_nossle_frames[3].p_xyz) / 2, 0, printer.jog_xyz_vel);
                                            printer.calibrate_nossle_frames.Add(fr_z_cal.clone());
                                            fr_z_cal.p_xyz.z += 15;
                                            //prog_cur.Add(new StepperFrame(new Point3d_GL(settins_string.calibrate_nossle_x[0], settins_string.calibrate_nossle_y[0], settins_string.calibrate_nossle_z[0]), 0, jog_xyz_vel));
                                            prog_cur.Add(fr_z_cal.clone());
                                            printer.start_alternate_prog(prog_cur.ToArray());


                                            printer.calibrate_nossle_stage_counter = 9;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);
                                        }


                                        //----CALIBR_Z------------------------------------------------------------------------------------

                                        if (printer.calibrate_nossle_stage_counter == 9 && printer.prog_state == StepperPrinter.programm_state.STOP)
                                        {
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "C1", false));
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(printer.calibrate_nossle_frames[printer.calibrate_nossle_frames.Count - 1].p_xyz, 0, printer.calibr_vel));
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrate_nossle_stage_counter = 10;
                                            Console.WriteLine("calibrate_nossle_stage_counter: " + printer.calibrate_nossle_stage_counter);

                                        }

                                        if (printer.calibrate_nossle_stage_counter == 10 && printer.calibr_z == 2)
                                        {
                                            printer.calibrate_nossle_frames.Add(printer.cur_frame.clone());
                                            var prog_cur = new List<StepperFrame>();
                                            prog_cur.Add(new StepperFrame(1, 589, "C0", false));
                                            var cur_fr_dz = printer.cur_frame.p_xyz.Clone();
                                            cur_fr_dz.z += 30;
                                            prog_cur.Add(printer.cur_frame.clone());
                                            prog_cur.Add(new StepperFrame(cur_fr_dz, 0, printer.jog_xyz_vel));

                                            var fr_x_cal = (printer.calibrate_nossle_frames[0].p_xyz + printer.calibrate_nossle_frames[1].p_xyz) / 2;
                                            var fr_y_cal = (printer.calibrate_nossle_frames[2].p_xyz + printer.calibrate_nossle_frames[3].p_xyz) / 2;
                                            printer.offset_nossle[printer.tool_active].p_xyz.x = fr_x_cal.x;
                                            printer.offset_nossle[printer.tool_active].p_xyz.y = fr_y_cal.y;
                                            printer.offset_nossle[printer.tool_active].p_xyz.z = printer.cur_frame.p_xyz.z;
                                            printer.tool_calibrated_auto[printer.tool_active] = true;
                                            printer.start_alternate_prog(prog_cur.ToArray());
                                            printer.calibrating_nossle = false;
                                            printer.cur_coordinate_system = printer.dest_coordinate_system;
                                            printer.offset_frame = printer.comp_offset(printer.cur_coordinate_system);

                                            Console.WriteLine("calibrate done");
                                        }






                                        /*if (calibrate_nossle_stage_counter >1 && prog_state == programm_state.STOP)
                                        {
                                            Console.WriteLine("calibrate failed stage_counter == "+ calibrate_nossle_stage_counter);

                                            calibrating_nossle = false;
                                        }*/

                                    }


                                }


                            }
                            //catch
                            {

                            }
                        }
                        // Console.WriteLine(commands1.Count);
                        if (commands1.Count > 0)
                        {

                            // var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);
                            var cur_num_ins = commands1[0].num - count_send1;
                            //Console.WriteLine("send1 com pre: " + cur_num_board + "/" + cur_num_ins+" "+ count_send1 + " " + commands1[0].com);

                            if (!initing1)
                            {
                                initing1 = true;
                                count_send1 = commands1[0].num - cur_num_board - 1;
                                cur_num_ins = commands1[0].num - count_send1;
                                //Console.WriteLine("init 1 f:" + dtime + " " + (last_time_1 - start_time));
                                //Console.WriteLine("count_send1 " + count_send1 + ";cur_num_ins " + cur_num_ins + "; cur_num_board " + cur_num_board + "; commands1[0].num " + commands1[0].num);
                            }

                            if (cur_num_ins - 1 == cur_num_board)
                            {
                                var com_cur = "N" + cur_num_ins + " " + commands1[0].com;
                                var mes_out = Encoding.ASCII.GetBytes(com_cur);
                                udp_client1.Send(mes_out, mes_out.Length);

                                //Console.WriteLine("send1 com: " + cur_num_board + "/" + cur_num_ins + " " + com_cur+" "+mes);
                            }
                            else if (cur_num_ins == cur_num_board)
                            {
                                commands1.RemoveAt(0);

                                // count_send1++;
                                //Console.WriteLine("send1 else if: " + cur_num_board + "/" + cur_num_ins);
                            }
                            else
                            {

                                //Console.WriteLine("send1 else: " + cur_num_board + "/" + cur_num_ins);
                            }
                        }
                    }
                }

                if (udp_client2 != null)
                    if (udp_client2.Available > 0)
                    {


                        var res = udp_client2.Receive(ref udp_addres_2);

                        long dtime = DateTime.Now.Ticks - last_time_2;
                        last_time_2 = DateTime.Now.Ticks;
                        if (dtime > 5000000)
                        {
                            initing2 = false;
                            Console.WriteLine("init 2 re:" + dtime + " " + (last_time_2 - start_time));
                        }
                        //Console.WriteLine(DateTime.Now.Ticks);

                        var mes = Encoding.ASCII.GetString(res) + "\n";
                        //Console.WriteLine("res2: " + mes);

                        if (res != null && !mes.Contains('M'))
                        {
                            var vars_from_mes = mes.Split(' ');
                            var cur_num_board = (long)Convert.ToInt32(vars_from_mes[1]);

                            //Console.WriteLine(vars_from_mes.Length);
                            if (vars_from_mes.Length >= 9)
                            {
                                //try
                                {

                                    var cur_send = (long)Convert.ToInt32(vars_from_mes[3]);
                                    if (vars_from_mes[4].Length == 5)
                                    {
                                        //Console.WriteLine("2:" + vars_from_mes[4]);
                                        //wait_term = Convert.ToInt32(vars_from_mes[4][3]) - 48;
                                        printer.wait_term = Convert.ToInt32(vars_from_mes[4][4]) - 48;
                                        printer.all_steps2 = Convert.ToInt32(vars_from_mes[4][0]) - 48;
                                    }
                                    

                                }
                                //catch
                                {

                                }
                            }
                            if (_TCPserver1.connected)
                            {
                                _TCPserver1.pushBuffer(mes);
                            }

                            if (commands2.Count > 0 && !mes.Contains('M') && !mes.Contains('N'))
                            {

                                //var cur_num_board = (long)Convert.ToInt32(mes.Split(' ')[1]);
                                //Console.WriteLine("send1 com: " + cur_num_board + "/" + count_send1 + " " + coms1[0]);
                                var cur_num_ins = commands2[0].num - count_send2;
                                //Console.WriteLine("send2 com pre: " + cur_num_board + "/" + cur_num_ins + " " + count_send2 + " " + commands2[0].com);
                                if (!initing2)
                                {
                                    initing2 = true;
                                    count_send2 = commands2[0].num - cur_num_board - 1;
                                    cur_num_ins = commands2[0].num - count_send2;
                                    //Console.WriteLine("count_send1 " + count_send1 + ";cur_num_ins " + cur_num_ins + "; cur_num_board " + cur_num_board + "; commands1[0].num " + commands1[0].num);
                                    //Console.WriteLine("init 2 f:" + dtime + " " + (last_time_2 - start_time));
                                }

                                if (cur_num_ins - 1 == cur_num_board)
                                {
                                    var com_cur = "N" + cur_num_ins + " " + commands2[0].com;
                                    var mes_out = Encoding.ASCII.GetBytes(com_cur);
                                    udp_client2.Send(mes_out, mes_out.Length);

                                    //Console.WriteLine("send2 com: " + cur_num_board + "/" + cur_num_ins + " " + com_cur);
                                }
                                else if (cur_num_ins == cur_num_board)
                                {
                                    commands2.RemoveAt(0);

                                    // count_send1++;
                                    //Console.WriteLine("send1 else if: " + cur_num_board + "/" + cur_num_ins);
                                }
                                else
                                {

                                    //Console.WriteLine("send1 else: " + cur_num_board + "/" + cur_num_ins);
                                }
                            }

                        }
                    }


                    //Console.
                    // if (_TCPserver1.connected) _TCPserver1.handle();

                    //if (com_num > 1) Console.WriteLine(com_num);

                }


            }
        

        public void load_settings()
        {
            var st = load_obj<SettingsString>("settings_printer.json");
            if (st == null)
            {
                Console.WriteLine("settings_printer.json не найден или повреждён — создаю с настройками по умолчанию");
                st = new SettingsString();
            }
            st.fill_missing();
            printer.settins_string = st;
            st.apply_planner_settings();

            // Fabion: одна плата, 8 моторов: 0 X, 1 Y, 2..4 Z1..Z3, 5..7 E1..E3
            for (int i = 0; i < st.motors_count1; i++)
            {
                _TCPserver1.pushBuffer_in("num1 M587" +
                    " I" + i +
                    " A" + Math.Round(st.a_max1[i], 3) +
                    " V" + Math.Round(st.v_def1[i], 3) +
                    " E" + st.end_inv1[i] +
                    " D" + st.motor_dir1[i] +
                    " R" + st.steps_per_mm1[i] +
                    " K" + st.home_dir1[i] +
                    "\n");
            }
            for (int i = 0; i < st.motors_count1; i++)
            {
                _TCPserver1.pushBuffer_in("num1 M587" +
                    " I" + i +
                    " X" + st.soft_min_pos1[i] +
                    " Y" + st.soft_max_pos1[i] +
                    " Z1" +
                    "\n");
            }
            // активный шприц в буфере платы
            _TCPserver1.pushBuffer_in("num1 M588 K" + printer.z_motor() + " L" + printer.e_motor() + "\n");
            save_obj("settings_printer.json", st);
        }

        // Кадр «поднять Z шприца tool в парковку» (машинные мм, settings: z_park_mm)
        StepperFrame park_z_frame(int tool, bool wait)
        {
            var zm = printer.z_motor(tool);
            var z = printer.settins_string.z_park_mm[Math.Clamp(tool, 0, StepperPrinter.TOOLS_COUNT - 1)];
            return new StepperFrame(1, 587, "I" + zm + " P" + z.ToString(System.Globalization.CultureInfo.InvariantCulture), false, wait ? 1 : 0);
        }

        static string fmt_mm(double v)
        {
            return Math.Round(v, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        static int val_from_command(string cmd)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var var = Convert.ToInt32(vars[1]);
            return var;
        }
        static double val_from_command_d(string cmd)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var var = Convert.ToDouble(vars[1]);
            return var;
        }

        static double[] vals_from_command_d(string cmd, int len)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var vals = new double[len];
            for(int i=0;  i<len; i++)
            {
                vals[i] = Convert.ToDouble(vars[1+i]);
            }
            
            return vals;
        }


        static int[] vals_from_command(string cmd, int len)
        {
            var command_af = cmd.Replace("  ", " ");
            var vars = command_af.Trim().Split(' ');
            var vals = new int[len];
            for (int i = 0; i < len; i++)
            {
                vals[i] = Convert.ToInt32(vars[1 + i]);
            }

            return vals;
        }
        // Режим MJPG по камерам: {ширина, высота}. Камера должна иметь такой режим MJPG
        // (у A4tech FHD: 1920x1080, 1280x720, 800x600, 640x480, 640x360, 320x240 — все 30 к/с).
        static int[,] cam_size = { { 1920, 1080 }, { 1920, 1080 }, { 640, 480 } };

        Thread start_cam(int ind, int port)
        {
            // Кадр JPEG от камеры уходит в UDP как есть: без распаковки и повторного сжатия.
            // Отдельный поток не нужен — кадры приходят из потока DirectShow (событие FrameReady).
            var cam = CameraMjpg.Open(ind, cam_size[ind, 0], cam_size[ind, 1], 30, out string err);
            if (cam == null)
            {
                Console.WriteLine("cam" + ind + ": камера не открылась: " + err);
                return null;
            }
            cam.SetExposure(-7);
            Console.WriteLine("cam" + ind + ": " + cam.Name + ", MJPG " + cam.Width + "x" + cam.Height + " " + cam.Fps + " fps");

            var udpSender = new UdpClient();
            udpSender.Client.SendBufferSize = 4 * 1024 * 1024;   // кадр FullHD уходит пачкой частей
            uint frame_id = 0;
            cam.FrameReady += (c, jpeg) =>
            {
                var client = _TCPserver1.get_client();
                if (client == null) return;
                try
                {
                    send_frame_udp(udpSender, new IPEndPoint(client.Address, ports_cam[ind]), jpeg, frame_id++);
                }
                catch (Exception e) { Console.WriteLine("cam" + ind + ": " + e.Message); }
            };
            _cameras[ind] = cam;
            _isStreaming[ind] = true;
            Console.WriteLine("Начало видеопотока через UDP...  " + ind);
            return null;
        }

        // Распакованные последние кадры камер — только по запросу (калибровка по изображению)
        Mat[] decode_last_frames()
        {
            var res = new Mat[_cameras.Length];
            for (int i = 0; i < _cameras.Length; i++)
                res[i] = _cameras[i]?.DecodeLast();
            return res;
        }

        public void auto_setup_cams()
        {
            var frms_st =decode_last_frames();
            CvInvoke.Imshow("st1", frms_st[1]);
            _TCPserver1.pushBuffer_in("M585 A R0\n");
            _TCPserver1.pushBuffer_in("M585 C R0\n");
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 A R255\n");
            Thread.Sleep(500);
            var frms_bef = decode_last_frames();
            CvInvoke.Imshow("bef1", frms_bef[1]);
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 A R0\n");
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 C R255\n");
            Thread.Sleep(500);
            _TCPserver1.pushBuffer_in("M585 C R0\n");
            var frms_aft = decode_last_frames();

            var delt_bef = comp_delt_mats(frms_st, frms_bef);
            var delt_aft = comp_delt_mats(frms_st, frms_aft);

            int maxIndex_bef = Array.IndexOf(delt_bef, delt_bef.Max());
            int maxIndex_aft = Array.IndexOf(delt_aft, delt_aft.Max());

            ports_cam[maxIndex_bef] = 5000;
            ports_cam[maxIndex_aft] = 5001;
            var vals_used = new bool[] { false, false, false };
            vals_used[maxIndex_bef] = true;
            vals_used[maxIndex_aft] = true;
            for(int i=0; i<vals_used.Length;i++)
            {
                if (!vals_used[i])
                {
                    ports_cam[i] = 5002;
                }
            }
        }

        

        public static double[] comp_delt_mats(Mat[] frames_st, Mat[] frames_past)
        {
            var delts = new double[frames_st.Length];
            for(int i=0; i<frames_st.Length;i++)
            {
                if(frames_past[i]!=null && frames_st[i]!=null)
                {
                    Mat delt_mat = frames_past[i] - frames_st[i];
                    CvInvoke.CvtColor(delt_mat, delt_mat, ColorConversion.Rgb2Gray);
                    delts[i] = delt_mat.ToImage<Gray, byte>().GetAverage().Intensity;
                }
                
            }
            return delts;
        }
        const int FRAG_PAYLOAD = 60000;   // одна машина (127.0.0.1); по сети лучше 1400 — без IP-фрагментации
        static void send_frame_udp(UdpClient udp, IPEndPoint ep, byte[] jpeg, uint frame_id)
        {
            int count = (jpeg.Length + FRAG_PAYLOAD - 1) / FRAG_PAYLOAD;
            if (count > ushort.MaxValue) return;
            byte[] pkt = new byte[12 + FRAG_PAYLOAD];
            pkt[0] = (byte)'B'; pkt[1] = (byte)'P'; pkt[2] = (byte)'F'; pkt[3] = (byte)'1';
            pkt[4] = (byte)(frame_id >> 24); pkt[5] = (byte)(frame_id >> 16);
            pkt[6] = (byte)(frame_id >> 8); pkt[7] = (byte)frame_id;
            pkt[10] = (byte)(count >> 8); pkt[11] = (byte)count;
            for (int i = 0; i < count; i++)
            {
                int off = i * FRAG_PAYLOAD;
                int len = Math.Min(FRAG_PAYLOAD, jpeg.Length - off);
                pkt[8] = (byte)(i >> 8); pkt[9] = (byte)i;
                Buffer.BlockCopy(jpeg, off, pkt, 12, len);
                udp.Send(pkt, 12 + len, ep);
            }
        }

        static byte[] FrameToJpegBytesEmgu(Mat frame, int quality = 70)
        {
            KeyValuePair<ImwriteFlags, int>[] encodeParams = new KeyValuePair<ImwriteFlags, int>[]
            {
            new KeyValuePair<ImwriteFlags, int>(ImwriteFlags.JpegQuality, quality)
            };
            byte[] buffer;
            using (VectorOfByte vector = new VectorOfByte())
            {
                CvInvoke.Imencode(".jpg", frame, vector, encodeParams);
                buffer = vector.ToArray();
            }
            return buffer;

        }

        static public void save_obj(string path, object obj)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
            using (StreamWriter sw = new StreamWriter(path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, obj);
            }
        }
        static public T load_obj<T>(string path, string text = null)
        {
            string jsontext = "";

            try
            {
                if (text != null)
                {
                    jsontext = text;
                }
                else
                {
                    using (StreamReader file = File.OpenText(path))
                    {
                        jsontext = file.ReadToEnd();
                    }
                    // Console.WriteLine(path + "__________________________");
                    //Console.WriteLine(jsontext);
                }
                return JsonConvert.DeserializeObject<T>(jsontext);
            }
            catch
            {
                return default(T);
            }

        }

    }


    class Command
    {

        public long num;
        public string com;
        public Command(long num, string com)
        {
            this.com = com;
            this.num = num;
        }

    }

    public class SettingsString
    {
        //планировщик
        public double max_acs;
        public double max_r;
        public double min_dist;

        //настройки моторов
        public int motors_count1 = 8;

        public double[] a_max1;
        public double[] v_def1;
        public int[] end_inv1;
        public int[] motor_dir1;
        public int[] steps_per_mm1;
        public int[] home_dir1;
        public int[] home_pos1;
        public int[] soft_max_pos1;
        public int[] soft_min_pos1;

        public int motors_count2 = 0;   // Fabion: платы 2 нет

        // Fabion: высота парковки Z1..Z3 (машинные мм) — куда поднимается неактивный шприц
        public double[] z_park_mm;

        public double[] a_max2;
        public double[] v_def2;
        public int[] end_inv2;
        public int[] motor_dir2;
        public int[] steps_per_mm2;
        public int[] home_dir2;
        public int[] home_pos2;
        public int[] soft_max_pos2;
        public int[] soft_min_pos2;
        //------------------------------------------
        public int[] take_left_manip_rot;
        public int[] take_left_manip_vert;

        public double[] take_left_manip_x;
        public double[] take_left_manip_y;
        public double[] take_left_manip_z;

        //------------------------------------------
        public int[] take_right_manip_rot;
        public int[] take_right_manip_vert;

        public double[] take_right_manip_x;
        public double[] take_right_manip_y;
        public double[] take_right_manip_z;


        //------------------------------------------

        public double[] calibrate_nossle_x;
        public double[] calibrate_nossle_y;
        public double[] calibrate_nossle_z;

        public double[] calibrate_nossles_offset_z;
        //------------------------------------------

        public double[] offset_plate_x;
        public double[] offset_plate_y;
        public double[] offset_plate_z;

        public double offset_las_sys_x = 0;
        public double offset_las_sys_y = 0;
        public double offset_las_sys_z = 0;



        //------------------------------------------

        public int table_work_pos   = 0;
        public int table_change_pos = 0;
        public int table_open_pos = 0;
        public int lift_up_val      = 0;

        public int servo_open_val = 0;
        public int servo_close_val = 0;

        public int servo_osc1_open_val = 0;
        public int servo_osc1_close_val = 0;

        public int servo_osc2_open_val = 0;
        public int servo_osc2_close_val = 0;

        public int osc_rot_start_pos1 = 0;
        public int osc_rot_start_pos2 = 0;



        public SettingsString()
        {
            a_max1 =  new double[motors_count1];
            v_def1 =  new double[motors_count1];
            end_inv1 =  new int[motors_count1];
            motor_dir1 = new int[motors_count1];
            steps_per_mm1 = new int[motors_count1];
            home_dir1 =  new int[motors_count1];
            home_pos1 =  new int[motors_count1];

            a_max2 =  new double[motors_count2];
            v_def2 =  new double[motors_count2];
            end_inv2 =  new int[motors_count2];
            motor_dir2 = new int[motors_count2];
            steps_per_mm2 = new int[motors_count2];
            home_dir2 =  new int[motors_count2];
            home_pos2 =  new int[motors_count2];

            for (int i = 0; i < motors_count1; i++)
            {
                a_max1[i]     = 2;
                v_def1[i]     = 2;
                end_inv1[i]   = 0;
                motor_dir1[i] = 1;
                steps_per_mm1[i] = 100;
                home_dir1[i]  = 1;
                home_pos1[i]  = 0;
            }

            for (int i = 0; i < motors_count2; i++)
            {
                a_max2[i]     = 2;
                v_def2[i]     = 2;
                end_inv2[i]   = 0;
                motor_dir2[i] = 1;
                steps_per_mm2[i] = 100;
                home_dir2[i]  = 1;
                home_pos2[i]  = 0;
            }
        }

        // Fabion: заполнить отсутствующие в json поля значениями по умолчанию
        public void fill_missing()
        {
            int n = motors_count1;
            double[] D(double[] a, double v) { if (a != null && a.Length >= n) return a; var r = new double[n]; for (int i = 0; i < n; i++) r[i] = (a != null && i < a.Length) ? a[i] : v; return r; }
            int[] I(int[] a, int v) { if (a != null && a.Length >= n) return a; var r = new int[n]; for (int i = 0; i < n; i++) r[i] = (a != null && i < a.Length) ? a[i] : v; return r; }
            a_max1 = D(a_max1, 2); v_def1 = D(v_def1, 2);
            end_inv1 = I(end_inv1, 0); motor_dir1 = I(motor_dir1, 1); steps_per_mm1 = I(steps_per_mm1, 100);
            home_dir1 = I(home_dir1, 1); home_pos1 = I(home_pos1, 0);
            soft_min_pos1 = I(soft_min_pos1, 0); soft_max_pos1 = I(soft_max_pos1, 100000);
            if (z_park_mm == null || z_park_mm.Length < 3) z_park_mm = new double[] { 0, 0, 0 };
            if (calibrate_nossle_x == null || calibrate_nossle_x.Length < 3) calibrate_nossle_x = new double[] { 0, 0, 0 };
            if (calibrate_nossle_y == null || calibrate_nossle_y.Length < 3) calibrate_nossle_y = new double[] { 0, 0, 0 };
            if (calibrate_nossle_z == null || calibrate_nossle_z.Length < 3) calibrate_nossle_z = new double[] { 0, 0, 0 };
            if (calibrate_nossles_offset_z == null) calibrate_nossles_offset_z = new double[] { 0, 0, 0 };
            if (offset_plate_x == null || offset_plate_x.Length < 4) offset_plate_x = new double[] { 0, 0, 0, 0 };
            if (offset_plate_y == null || offset_plate_y.Length < 4) offset_plate_y = new double[] { 0, 0, 0, 0 };
            if (offset_plate_z == null || offset_plate_z.Length < 4) offset_plate_z = new double[] { 0, 0, 0, 0 };
            if (max_acs <= 0) max_acs = 50;
            if (max_r <= 0) max_r = 0.3;
            if (min_dist <= 0) min_dist = 0.1;
        }

        public void apply_planner_settings()
        {
            StepperPrinter.min_dist = min_dist;
            StepperPrinter.printer_max_r = max_r;
            StepperPrinter.printer_max_acs = max_acs;
        }

    }


}

