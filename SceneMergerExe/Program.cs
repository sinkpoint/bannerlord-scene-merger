using System;
using System.Xml;
using SpSceneMerger;

namespace SceneMerger
{
    class Program
    {
        static void Main(string[] args)
        {
            var f1 = args[0];
            var f2 = args[1];

            //var f1 = "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\SceneObj\\Main_map\\scene.xscene";
            //var f2 = "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SpHuaxia\\SceneObj\\Main_map\\scene.xscene";

            foreach (var i in args)
            {
                Console.WriteLine(i);
            }

            try
            {
                XmlDocument xdoc1 = new XmlDocument();
                XmlDocument xdoc2 = new XmlDocument();
                xdoc1.Load(f1);
                xdoc2.Load(f2);

                XmlDocument xdocm = Merger.MyXmlMerge(xdoc1, xdoc2);
                xdocm.Save("scene.xscene");

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }
    }
}