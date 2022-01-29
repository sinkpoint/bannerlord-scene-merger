using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using System.Threading.Tasks;
using System.Numerics;

namespace SpSceneMerger
{
    class GameEntityComparitor : IEqualityComparer<XmlElement>
    {
        public bool Equals(XmlElement x, XmlElement y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.GetAttribute("name").Equals(y.GetAttribute("name"));
        }

        public int GetHashCode(XmlElement obj)
        {
            if (Object.ReferenceEquals(obj, null)) return 0;

            return obj.GetAttribute("name").GetHashCode();
        }
    }

    public class EntryModule : MBSubModuleBase
    {
        /**
         *  Merge scene xml, then load self
         */
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            EntryModule self = this;

            InitialStateOption initStateOpt = new InitialStateOption(
                            "ExampleModule",
                            new TextObject("Merge World Maps", null),
                            9990,
                            () => Task.Factory.StartNew(() => DoMerge()),
                            () => (false, new TextObject("Merge Disabled"))
                        );

            Module.CurrentModule.AddInitialStateOption(initStateOpt);
        }

        public static void DoMerge()
        {
            MBDebug.ConsolePrint("************** SpSceneMerger Start **************");

            String MY_MODULE_NAME = "SpSceneMerger";
            string basepath = Utilities.GetBasePath();
            String[] mod_paths = Utilities.GetFullModulePaths();
            String scenepath = @"/SceneObj/Main_map/scene.xscene";
            String msg = "";

            XmlDocument xdocbase = null;

            foreach (var modpath in mod_paths)
            {
                String fullscenepath = modpath.Replace("$BASE/", basepath) + scenepath;

                if (modpath.Contains(MY_MODULE_NAME))
                    continue;

                if (File.Exists(fullscenepath))
                {
                    // do merge
                    msg = "found main scene " + fullscenepath;
                    Console.WriteLine(msg);
                    MBDebug.ConsolePrint(msg);
                    InformationManager.DisplayMessage(new InformationMessage("found map for "+modpath.Split('/')[2]));

                    if (xdocbase == null)
                    {
                        xdocbase = new XmlDocument();
                        xdocbase.Load(fullscenepath);
                    }
                    else
                    {
                        XmlDocument xdocnew = new XmlDocument();
                        xdocnew.Load(fullscenepath);
                        xdocbase = Merger.MyXmlMerge(xdocbase, xdocnew);
                    }
                }
            }

            String mypath = Utilities.GetFullModulePath("SpSceneMerger").Replace("$BASE/", basepath);
            msg = "save final merge to " + mypath + scenepath;
            //Console.WriteLine(msg);
            MBDebug.ConsolePrint(msg);

            xdocbase.Save(mypath + scenepath);

            MBDebug.ConsolePrint("************** SpSceneMerger End **************");
            InformationManager.DisplayMessage(new InformationMessage("World scene merge complete, please restart the game.",Colors.Red));
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage("Scene Merger loaded"));
        }
    }

    public class Merger
    {

        public static void  doMergeDifference(XmlNodeList xl1, XmlNodeList xl2, String xpathroot, ref XmlDocument xdoc1, bool ig_ic=true)
        {
            Console.WriteLine("-- xl1 has {0} elements", xl1.Count);
            Console.WriteLine("-- xl2 has {0} elements", xl2.Count);

            HashSet<String> nameMap = new HashSet<string>();
            

            foreach (XmlNode i in xl1)
            {
                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    String node_name = i.Attributes["name"].Value;
                    Console.WriteLine("xl1 elt {0}", node_name);
                    nameMap.Add(node_name);
                }
            }

            // add unique base-level elements,
            ArrayList queue = new ArrayList();
            foreach (XmlNode i in xl2)
            {
                String nodename = i.Attributes["name"].Value;
                Console.WriteLine("xl2 elt {0}", nodename);
                if (ig_ic && nodename.StartsWith("campaign_icon_capsule"))
                {
                    continue;
                }

                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    if ( !nameMap.Contains(nodename) )
                    {
                        queue.Add(i);
                    }
                }
            }

            XmlNode ent = xdoc1.SelectSingleNode(xpathroot);

            foreach (XmlNode i in queue)
            {
                XmlNode j = xdoc1.ImportNode(i, true);
                ent.AppendChild(j);
            }
        }

        public static void doMergeOverlap(XmlNodeList xl1, XmlNodeList xl2, String xpathroot, ref XmlDocument xdoc1)
        {
            Console.WriteLine(xpathroot);
            XmlNode ent = xdoc1.SelectSingleNode(xpathroot);
            // find nodes in doc2 that overlaps with doc1, and merge them in
            var nl = xl2.Cast<XmlElement>().Intersect<XmlElement>(xl1.Cast<XmlElement>(), new GameEntityComparitor()).ToList();

            foreach (XmlElement i in nl)
            {
                String node_name = i.GetAttribute("name");
                if (node_name == "")
                    continue;

                XmlNode jn = xdoc1.ImportNode(i, true);
                String qpath = $"./game_entity[@name='{node_name}']";
                Console.WriteLine("-qpath {0}",qpath);
                XmlNode elt1 = ent.SelectSingleNode(qpath);
                ent.ReplaceChild(jn, elt1);
            }
        }

        public static XmlDocument MyXmlMerge(XmlDocument xdoc1, XmlDocument xdoc2)
        {
            XmlNodeList xl1 = xdoc1.SelectNodes("/scene/entities/game_entity[not(starts-with(@name, 'campaign_icon_capsule'))]");
            XmlNodeList xl1_ic = xdoc1.SelectNodes("/scene/entities/game_entity[starts-with(@name, 'campaign_icon_capsule')]");
            XmlNodeList xl1_ic_children = xdoc1.SelectNodes("/scene/entities/game_entity[starts-with(@name, 'campaign_icon_capsule')]/children//game_entity");

            XmlNodeList xl2 = xdoc2.SelectNodes("/scene/entities/game_entity[not(starts-with(@name, 'campaign_icon_capsule'))]");
            XmlNodeList xl2_ic = xdoc2.SelectNodes("/scene/entities/game_entity[starts-with(@name, 'campaign_icon_capsule')]");
            //XmlNodeList xl2_ic_children = xdoc2.SelectNodes("/scene/entities/game_entity[starts-with(@name, 'campaign_icon_capsule')]/children//game_entity");

            Console.WriteLine("doc1 has {0} elements", xl1.Count);
            Console.WriteLine("doc1 has {0} ic elements", xl1_ic.Count);
            Console.WriteLine("doc1 has {0} ic children elements", xl1_ic_children.Count);
            Console.WriteLine("doc2 has {0} elements", xl2.Count);
            Console.WriteLine("doc2 has {0} ic elements", xl2_ic.Count);

            // find brand new nodes in doc2, and save to doc1
            //var nl = xl2.Cast<XmlElement>().Except<XmlElement>(xl1.Cast<XmlElement>(), new GameEntityComparitor()).ToList();
            //Console.WriteLine("diff {0} elements", nl.Count);

            // node name in new file can be non-unique, manual lookup needed

            HashSet<String> nameMap = new HashSet<string>();
            IDictionary<String, XmlNode> icnameMap = new Dictionary<string, XmlNode>();

            foreach (XmlNode i in xl1)
            {
                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    nameMap.Add(i.Attributes["name"].Value);
                }
            }

            foreach (XmlNode i in xl1_ic_children)
            {
                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    icnameMap[i.Attributes["name"].Value] = i;
                    Console.WriteLine("ic1 {0}",i.Attributes["name"].Value);
                }
            }

            String[] exclude = new String[] { "empty_object", "editor_cube", "", "_decal" };
            // add unique base-level elements,
            ArrayList queue = new ArrayList();
            foreach( XmlNode i in xl2 )
            {
                //if (nodename.StartsWith("campaign_icon_capsule")) {
                //    continue;
                //}

                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    String nodename = i.Attributes["name"].Value;
                    if ( ! (nameMap.Contains(nodename) || icnameMap.ContainsKey(nodename)) ) 
                    {
                        queue.Add(i);
                    }
                    else if (icnameMap.ContainsKey(nodename))
                    {
                        if (!exclude.Any(s => s.Equals(nodename))) {

                            // overwrite into the ic
                            Console.WriteLine("ic contains {0}", nodename);
                            XmlNode imnode = icnameMap[nodename];
                            if (imnode.ParentNode == null)
                                continue;

                            XmlNode icparent = imnode.ParentNode;
                            XmlNode icroot = icparent.ParentNode;
                            XmlNode parentTransform = icroot.SelectSingleNode("./transform");
                            Vec3 parentpos = Vec3.Parse(parentTransform.Attributes["position"].Value);
                            XmlNode localTransform = i.SelectSingleNode("./transform");
                            Vec3 localpos = Vec3.Parse(localTransform.Attributes["position"].Value);
                            Console.WriteLine("old pos: {0}", localpos.ToString());
                            Vec3 newpos = localpos - parentpos;
                            localTransform.Attributes["position"].Value = $"{newpos.X}, {newpos.Y}, {newpos.Z}";
                            Console.WriteLine("new pos: {0}",localTransform.Attributes["position"].Value);
                            XmlNode j = xdoc1.ImportNode(i, true);
                            icparent.ReplaceChild(j, icnameMap[nodename]);
                        }
                    }
                }
            }

            XmlNode ent = xdoc1.SelectSingleNode("/scene/entities");

            foreach (XmlNode i in queue)
            {
                Console.WriteLine(i.Attributes["name"].Value);
                XmlNode j = xdoc1.ImportNode(i, true);
                ent.AppendChild(j);
            }

            // find nodes in doc2 that overlaps with doc1, and merge them in

            doMergeOverlap(xl1, xl2, "/scene/entities", ref xdoc1);

            //var nl = xl2.Cast<XmlElement>().Intersect<XmlElement>(xl1.Cast<XmlElement>(), new GameEntityComparitor()).ToList();

            //foreach (XmlElement i in nl)
            //{
            //    String node_name = i.GetAttribute("name");
            //    if (node_name == "")
            //        continue;

            //    if (node_name.StartsWith("campaign_icon_capsule"))
            //        continue;

            //    XmlNode jn = xdoc1.ImportNode(i, true);
            //    XmlNode elt1 = ent.SelectSingleNode($"game_entity[@name='{node_name}']");
            //    ent.ReplaceChild(jn, elt1);
            //}

            // -------------------- merge each capsule -------------------------------
            Console.WriteLine("------------ IC -----------");

            doMergeDifference(xl1_ic, xl2_ic, "/scene/entities", ref xdoc1, false);

            IDictionary<String, XmlNode> icMap = new Dictionary<String, XmlNode>();
            foreach (XmlNode i in xl1_ic)
            {
                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    icMap.Add(i.Attributes["name"].Value, i);
                }
            }

            foreach (XmlNode i in xl2_ic)
            {
                if (i.Attributes != null && i.Attributes["name"] != null)
                {
                    String node_name = i.Attributes["name"].Value;
                    if (icMap.ContainsKey(node_name))
                    {
                        XmlNode j = icMap[node_name];
                        XmlNodeList ic2 = i.SelectNodes("./children/game_entity");
                        Console.WriteLine(ic2.Count);
                        XmlNodeList ic1 = j.SelectNodes("./children/game_entity");
                        Console.WriteLine(ic1.Count);
                        doMergeDifference(ic1, ic2, $"/scene/entities/game_entity[@name='{node_name}']/children", ref xdoc1);
                        doMergeOverlap(ic1, ic2, $"/scene/entities/game_entity[@name='{node_name}']/children", ref xdoc1);
                    }
                }
            }

            return xdoc1;
        }
    }
}
