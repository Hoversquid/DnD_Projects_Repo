using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LootTable
{

    // A MagicItemTable is defined by an XML file, but will eventually be able to link to other files.
    public class MagicItemTable
    {
        private string tableFileName = @"WeaponTable.xml";
        private XElement root;

        // ItemVars are defined at the beginning of the file, defining the values that are to be saved and also used as part of the generation.
        public XElement ItemVars { get; set; }

        // The Table node is where the initial query begins and contains all of the nodes that are accessed through rolls.
        public XElement Table { get; set; }

        // The IndexTable is used after the initial values are determined to translate certain numbers into ItemVars
        // ex: Plus_Price into Price
        public XElement IndexTable { get; set; }

        // The dice class
        public Random RollDie { get; set; }

        public MagicItemTable()
        {
            RollDie = new Random();
            root = XElement.Load(tableFileName);

            ItemVars = root.Element("ItemVars");
            Table = root.Element("Table");
            IndexTable = root.Element("IndexTable");

            // Sets the item to have initial values
            ClearItem();
        }

        public void ClearItem()
        {
            // Simply sets ItemVar values to an initial value
            foreach (XElement tableVar in ItemVars.Elements())
            {
                string type = (string)tableVar.Attribute("type");
                if ((type == "number"))
                {
                    tableVar.Value = "0";
                }
                else if (type == "string")
                {
                    tableVar.Value = "";
                }
                else if (type == "list")
                {
                    tableVar.Elements().Remove<XElement>();
                }
            }
        }

        public void DefaultRollOnTable()
        {
            Console.WriteLine("Beginning roll!");
            // create element to keep track of rerolls and internal variables
            XElement rollVars = new XElement("RollVars");

            // Tables are set to be default or not, to make the initial generation and determine the next set of rolls.
            // This selects the default nodes to use for generation.
            IEnumerable<XElement> defaultRolls =
                from defaultRoll in Table.Elements()
                where defaultRoll.Attribute("default").Value == "true"
                select defaultRoll;

            // rolls on all default tables	
            foreach (XElement tableRoll in defaultRolls)
            {
                TableRoll(tableRoll, rollVars);
            }

            // rolls on tables designated through the default rolls, and continues to roll on additional added tables
            foreach (XElement rollEle in rollVars.Elements())
            {
                XElement tableRoll = Table.Element((string)rollEle);
                int amtToRoll = (int)rollEle.Attribute("amt");

                for (int i = 0; i < amtToRoll; i++)
                {
                    TableRoll(tableRoll, rollVars);
                }
            }

            UseIndex();
        }

        public void TableRoll(XElement roll, XElement rollVars)
        {
            int rerollAmt = 0;
            XElement firstNode = roll.Elements().FirstOrDefault();

            // SelectNode() is a recursive function that serves as the meat of the query.
            // The function digs into the Table node to find the XElement that will be used to save to the item (the one containing the loot).
            XElement nodeToSelect = SelectNode(firstNode);

            if (nodeToSelect.Name.ToString() != "Empty")
            {

                // Once the SelectNode has dug deep enough, it checks if the changes are going to be valid.
                if (nodeToSelect.Elements().FirstOrDefault().Name.ToString() == "ItemVarCheck")
                {
                    // ItemVarCheck() runs through the loot nodes to see if the changes are going to be allowed based on the conditions defined in the Element.
                    // It either selects the loot node or a node labeled "Default" based on the pending changes.
                    nodeToSelect = ItemVarCheck(nodeToSelect.Elements().FirstOrDefault());
                }

                // Goes through each loot element and performs an appropriate action.
                foreach (XElement lootNode in nodeToSelect.Elements())
                {
                    string lootAction = lootNode.Name.ToString();
                    string lootItemVar = (string)lootNode;

                    switch (lootAction)
                    {

                        // AddTable nodes will either add a table to be rolled afterward, or will reroll the current table.
                        case "AddTable":
                            if (lootItemVar == roll.Name.ToString())
                            {
                                rerollAmt += 1;
                            }
                            else
                            {
                                rollVars.Add(lootNode);
                            }
                            break;

                        // Save nodes either change the value of a string variable in the ItemVars or add a string to a list in the ItemVars
                        case "Save":
                            XElement itemVar = ItemVars.Element(lootItemVar);
                            SaveToItem(lootNode, itemVar);
                            break;

                        // AddNum nodes add to a number value in the ItemVars
                        case "AddNum":
                            int amt = (int)lootNode.Attribute("amt");
                            int itemNum = (int)ItemVars.Element(lootItemVar);
                            ItemVars.Element(lootItemVar).Value = (itemNum + amt).ToString();
                            break;

                        default:
                            Console.WriteLine("Loot type " + lootAction + " not recognized.");
                            break;
                    }
                }
            }

            // If the table has indicated to reroll, call a nested function.
            if (rerollAmt > 0)
            {
                for (int i = 0; i < rerollAmt; i++)
                {
                    TableRoll(roll, rollVars);
                }
            }
        }

        // SelectNode() recursively calls itself based on a few keywords it encounters as it parses each node.
        public XElement SelectNode(XElement checkNode)
        {
            string name = checkNode.Name.ToString();

            // Categories will select an ItemVar and find the category node that has the same name as the ItemVar value.
            if (name == "Categories")
            {
                string categoryVar = (string)checkNode.Attribute("varName");

                // Iterates through the elements of the Categories node, looking for one named the same as the value of the ItemVar
                foreach (XElement category in checkNode.Elements())
                {
                    string categoryID = category.Name.ToString();
                    if ((string)ItemVars.Element(categoryVar) == categoryID)
                    {
                        string nextEleName = category.Elements().FirstOrDefault().Name.ToString();

                        // Category nodes may contain the actual loot nodes or additional selections
                        // This checks one element into the node to see if the function must be called again
                        if (nextEleName == "Categories" || nextEleName == "Roll")
                        {
                            return SelectNode(category.Elements().FirstOrDefault());
                        }
                        else
                        {
                            return category;
                        }
                    }
                }
                Console.WriteLine("No category match found when checking ItemVar " + categoryVar);
                return (new XElement("Empty"));
            }

            // Roll nodes get a type to roll (currently only percentile) and finds the first node that has a maxRoll higher than what was rolled.
            else if (name == "Roll")
            {
                string type = (string)checkNode.Attribute("type");
                if (type == "percentile")
                {
                    int randomRoll = RollDie.Next(100) + 1;

                    IEnumerable<XElement> rollResults =
                        from result in checkNode.Elements()
                        where (int)result.Attribute("maxRoll") >= randomRoll
                        select result;

                    if (rollResults.Count() > 0)
                    {
                        XElement result = rollResults.FirstOrDefault();
                        return SelectNode(result);
                    }
                    else
                    {
                        Console.WriteLine("Roll returned no results.");
                        return (new XElement("Empty"));
                    }
                }
                else
                {
                    Console.WriteLine("Roll type not recognized.");
                    return (new XElement("Empty"));
                }
            }
            else
            {
                // Here the function makes sure the next node doesn't require additional selection by checking its name.
                // Otherwise the function terminates with either the list of the loot nodes or an ItemVarCheck to make sure the loot is allowed to be saved.
                XElement nextNode = checkNode.Elements().FirstOrDefault();
                string nextNodeName = nextNode.Name.ToString();

                if (nextNodeName == "Categories" || nextNodeName == "Roll") { return SelectNode(nextNode); }
                else { return checkNode; }
            }
        }
        private XElement ItemVarCheck(XElement checkNode)
        {
            // gets the XElement to check from ItemVars
            string varName = (string)checkNode.Attribute("toCheck");
            XElement selectedItemVar = ItemVars.Element(varName);

            // checks type to use correct function on selected item
            string type = (string)selectedItemVar.Attribute("type");

            // selects the element that has the ItemVar name as the value
            IEnumerable<XElement> nodesToRead =
                    from node in checkNode.Elements()
                    select node;

            foreach (XElement node in nodesToRead)
            {
                if ((string)node == varName)
                {
                    if (type == "number")
                    {
                        // gets the number to check against itemvar and the operation to use for the check
                        int toCheck = (int)selectedItemVar;
                        int toMatch = (int)checkNode.Attribute("toMatch");
                        string operation = (string)checkNode.Attribute("op");
                        int amt = (int)node.Attribute("amt");

                        // does a boolean operation to determine if the loot items can be used
                        if (AddSuceed(toCheck, amt, operation, toMatch))
                        {
                            return checkNode;
                        }

                        // else, it will choose the "Default" node instead
                        else
                        {
                            Console.WriteLine("ItemVar will not be saved, using default node.");
                            return checkNode.Element("Default");
                        }
                    }
                    else if (type == "string")
                    {
                        // gets the ItemVar element and checks if its value equals the toMatch string
                        string toCheck = (string)selectedItemVar;
                        string toMatch = (string)checkNode.Attribute("toMatch");

                        if ((string)ItemVars.Element(toCheck) == toMatch)
                        {
                            return nodesToRead.FirstOrDefault();
                        }
                        else { return checkNode.Element("Default"); }
                    }
                    else
                    {
                        Console.WriteLine("Node type " + type + " not recognized.");
                        return checkNode.Element("Default");
                    }
                }
            }
            Console.WriteLine("ItemVar " + varName + " not found in selected nodes, selecting Default.");
            return checkNode.Element("Default");
        }
        private bool AddSuceed(int value, int addAmt, string operation, int checkValue)
        {
            // Tests the condition set by an ItemVarCheck's attributes
            // (TODO): Only does less than or equal (lte) right now, add more later 
            switch (operation)
            {
                case "lte":
                    if ((value + addAmt) <= checkValue) { return true; }
                    else { return false; }

                default:
                    Console.WriteLine("Operation " + operation + " not recognized.");
                    return false;
            }
        }
        private void SaveToItem(XElement lootVar, XElement itemVar)
        {
            // Gets the type of variable to save to in the ItemVars list (string, number, list)
            string lootVarName = lootVar.Name.ToString();
            string lootValue = (string)lootVar;
            string itemVarType = (string)itemVar.Attribute("type");
            switch (itemVarType)
            {
                case "string":
                    itemVar.Value = (string)lootVar.Attribute("toSave");
                    break;

                case "list":
                    itemVar.Add(new XElement(lootVarName, (string)lootVar.Attribute("toSave")));
                    break;

                default:
                    Console.WriteLine("Error saving " + lootVar.Name.ToString() + " to ItemVar " + itemVar.Name.ToString());
                    break;
            }
        }

        // UseIndex() translates an ItemVar (for now, as long as it's a number) into another number that will be saved to the item at the end of the query.
        void UseIndex()
        {
            foreach (XElement index in IndexTable.Elements())
            {
                if ((string)index.Attribute("type") == "number")
                {
                    int indexNum = (int)ItemVars.Element(index.Attribute("tableVar").Value);
                    IEnumerable<XElement> indexToSelect =
                        from indexEle in index.Elements()
                        where indexNum == (int)indexEle.Attribute("index")
                        select indexEle;

                    if (indexToSelect.Count() > 0)
                    {
                        XElement selected = indexToSelect.FirstOrDefault();
                        string itemVar = (string)selected;
                        int amt = (int)selected.Attribute("amt");
                        int itemNum = (int)ItemVars.Element(itemVar);
                        ItemVars.Element(itemVar).Value = (itemNum + amt).ToString();
                    }
                }

            }
        }
    }
}
