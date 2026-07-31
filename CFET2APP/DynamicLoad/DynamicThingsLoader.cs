using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using System.Reflection;
using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Log;
using Jtext103.CFET2.Core.Event;

namespace Jtext103.CFET2.CFET2App.DynamicLoad
{
    public class DynamicThingsLoader
    {
        private string dynamicThingsRootPath;

        private string dynamicDllsFilePath;

        private string separator;

        private List<ThingModel> thingModels;

        private List<string> thingDlls;

        private Cfet2Program cfetHost;

        Dictionary<string, Type> dllsDic = new Dictionary<string, Type>();

        private string thingDllPath = "./thingDll";

        private string thingConfigPath = "./thingConfig";

        private ICfet2Logger logger = Cfet2LogManager.GetLogger("DynamicLoad");

        public DynamicThingsLoader(Cfet2Program host)
        {
            cfetHost = host;

            separator = Path.DirectorySeparatorChar.ToString();
            //dynamicThingsRootPath = "." + separator + "DynamicLoad" + separator + "DynamicThing";
            //dynamicDllsFilePath = "." + separator + "DynamicLoad" + separator + "DynamicDll";

            thingModels = new List<ThingModel>();
            thingDlls = new List<string>();

            LoadAllThings(thingConfigPath);

            // try to load thingDll
            try { LoadAndCopyDllsInDir(thingDllPath); } catch { }

            SetDlls();

            AddAllThings();
        }

        //获取并存储一个文件夹下所有dll的路径并拷贝到执行目录，不会递归查找
        private void LoadAndCopyDllsInDir(string dirPath)
        {
            DirectoryInfo root = new DirectoryInfo(dirPath);

            foreach (FileInfo file in root.GetFiles())
            {
                if (file.Extension.ToLower() == ".dll")
                {
                    try
                    {
                        File.Copy(file.FullName, "." + separator + file.Name);
                    }
                    catch
                    {
                        //Console.WriteLine("Please note a dll copy failed: " + file.FullName);
                    }
                }
            }
        }

        //递归加载所有的Thing
        private void LoadAllThings(string rootPath)
        {
            DirectoryInfo root = new DirectoryInfo(rootPath);

            //搜索这一层所有文件
            foreach (FileInfo file in root.GetFiles())
            {
                //判断为一个Thing
                if (file.Name == "Config.json")
                {
                    var newThing = new ThingModel();
                    if (newThing.LoadConfig(file.FullName))
                    {
                        thingModels.Add(newThing);
                    }
                    else
                    {
                        Console.WriteLine("Failed to load Thing: " + root.Name + "!");
                    }

                    //如果有dll文件夹，加载其中的内容到dlls中
                    if (Directory.Exists(rootPath + separator + "dll"))
                    {
                        LoadAndCopyDllsInDir(rootPath + separator + "dll");
                    }
                }
            }

            //递归搜索子文件夹
            foreach (DirectoryInfo dir in root.GetDirectories())
            {
                LoadAllThings(dir.FullName);
            }
        }

        private void SetDlls()
        {
            DirectoryInfo root = new DirectoryInfo("./");

            foreach (FileInfo file in root.GetFiles())
            {
                if (file.Extension.ToLower() == ".dll")
                {
                    thingDlls.Add(file.FullName);
                }
            }
        }


        //将所有Thing实例化并挂载到CFET
        private void AddAllThings()
        {
            var builder = new ContainerBuilder();

            // 记录load失败的dll，并在后续获取type时跳过
            List<string> loadFailDlls = new List<string>();

            //添加所有dll
            foreach (var dll in thingDlls)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    // 新版本autofac更加严格，对于没有公共构造函数的类型无法注册
                    var types = assembly.GetTypes()
                                .Where(t => t.IsClass && !t.IsAbstract
                                        && t.GetConstructors().Any(c => c.IsPublic))
                                .ToArray();
                    builder.RegisterTypes(types);
                    builder.RegisterTypes(types).AsImplementedInterfaces();
                }
                catch
                {
                    loadFailDlls.Add(dll);
                    Console.WriteLine("Please note a dll load failed: " + dll);
                }
            }

            IContainer container;
            ILifetimeScope scope;

            try
            {
                container = builder.Build();
                scope = container.BeginLifetimeScope();
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }

            foreach (var dll in thingDlls)
            {
                if (loadFailDlls.Contains(dll)) continue;
                Type[] types = null;
                try
                {
                    types = Assembly.LoadFrom(dll).GetTypes();
                }
                catch (Exception e)
                {
                    throw new Exception(e.ToString() + " " + dll.ToString());
                }

                foreach (var type in types)
                {
                    dllsDic[type.FullName] = type;
                }
            }


            //添加每个Thing
            foreach (var thing in thingModels)
            {
                Type type = null;
                type = dllsDic[thing.Config.Type];

                try
                {
                    dynamic ins = scope.Resolve(type);
                    //type.GetType();
                    //添加配置文件路径
                    cfetHost.MyHub.TryAddThing(ins, thing.MountPath, thing.Name, thing.Config.InitObj, thingConfigPath);
                    logger.Info("AddThing:" + thing.Name);
                }
                catch (Exception e)
                {
                    throw new Exception(e.ToString());
                }
            }
        }
    }
}
