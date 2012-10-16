using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.SystemUI;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;


namespace ewasco_v4
{
    public sealed partial class MainForm : Form
    {
        #region class private members
        private IMapControl3 m_mapControl = null;
        private string m_mapDocumentName = string.Empty;
        #endregion

        #region Private Memebers
        /// <summary>
        /// This is used to bind our ITable to the binding source. We need to keep
        /// a reference to it as we will need to re-attach it to the binding source
        /// to force a refresh whenever we change from displaying coded value domain
        /// values to displaying their text equivalents and vice versa.
        /// </summary>
        private ArcDataBinding.TableWrapper tableWrapper;

        /// <summary>
        /// This binding object sets the data member within the data source for the 
        /// text box. We need to keep a reference to this as it needs to be reset
        /// whenever viewing of coded value domains is changed.
        /// </summary>
        private Binding txtBoxBinding;

        IFeatureLayer m_iFeatureLayer = null;
        IFeatureClass m_iFeatureClass = null;

        #endregion Private Memebers


        #region class constructor
        public MainForm()
        {
            InitializeComponent();
        }
        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            //get the MapControl
            m_mapControl = (IMapControl3)axMapControl1.Object;

            //disable the Save menu (since there is no document yet)
            menuSaveDoc.Enabled = false;
        }

        #region Main Menu event handlers
        private void menuNewDoc_Click(object sender, EventArgs e)
        {
            //execute New Document command
            ICommand command = new CreateNewDocument();
            command.OnCreate(m_mapControl.Object);
            command.OnClick();
        }

        private void menuOpenDoc_Click(object sender, EventArgs e)
        {
            //execute Open Document command
            ICommand command = new ControlsOpenDocCommandClass();
            command.OnCreate(m_mapControl.Object);
            command.OnClick();
        }

        private void menuSaveDoc_Click(object sender, EventArgs e)
        {
            //execute Save Document command
            if (m_mapControl.CheckMxFile(m_mapDocumentName))
            {
                //create a new instance of a MapDocument
                IMapDocument mapDoc = new MapDocumentClass();
                mapDoc.Open(m_mapDocumentName, string.Empty);

                //Make sure that the MapDocument is not readonly
                if (mapDoc.get_IsReadOnly(m_mapDocumentName))
                {
                    MessageBox.Show("Map document is read only!");
                    mapDoc.Close();
                    return;
                }

                //Replace its contents with the current map
                mapDoc.ReplaceContents((IMxdContents)m_mapControl.Map);

                //save the MapDocument in order to persist it
                mapDoc.Save(mapDoc.UsesRelativePaths, false);

                //close the MapDocument
                mapDoc.Close();
            }
        }

        private void menuSaveAs_Click(object sender, EventArgs e)
        {
            //execute SaveAs Document command
            ICommand command = new ControlsSaveAsDocCommandClass();
            command.OnCreate(m_mapControl.Object);
            command.OnClick();
        }

        private void menuExitApp_Click(object sender, EventArgs e)
        {
            //exit the application
            Application.Exit();
        }
        #endregion

        //listen to MapReplaced evant in order to update the statusbar and the Save menu
        private void axMapControl1_OnMapReplaced(object sender, IMapControlEvents2_OnMapReplacedEvent e)
        {
            //get the current document name from the MapControl
            m_mapDocumentName = m_mapControl.DocumentFilename;

            //if there is no MapDocument, diable the Save menu and clear the statusbar
            if (m_mapDocumentName == string.Empty)
            {
                menuSaveDoc.Enabled = false;
                statusBarXY.Text = string.Empty;
            }
            else
            {
                //enable the Save manu and write the doc name to the statusbar
                menuSaveDoc.Enabled = true;
                statusBarXY.Text = Path.GetFileName(m_mapDocumentName);
            }
        }

        private void axMapControl1_OnMouseMove(object sender, IMapControlEvents2_OnMouseMoveEvent e)
        {
            statusBarXY.Text = string.Format("{0}, {1}  {2}", e.mapX.ToString("#######.##"), e.mapY.ToString("#######.##"), axMapControl1.MapUnits.ToString().Substring(4));
        }

        #region Load Data

        private void bindTableToGrid()
        {

            //get meters featureclass
            if (m_iFeatureClass == null) { m_iFeatureClass = getFeatureClass(); }

            //get the attributes table for the particular feature class
            ITable iTable = (ITable)m_iFeatureClass;
            if (null != iTable)
            {
                // Bind dataset to the binding source
                tableWrapper = new ArcDataBinding.TableWrapper(iTable);
                tableWrapper.UseCVDomains = true;
                bindingSource1.DataSource = tableWrapper;

                // Bind binding source to grid. Alternatively it is possible to bind TableWrapper
                // directly to the grid to this offers less flexibility
                dataGridView1.DataSource = bindingSource1;

                // Bind binding source to text box, we are binding the NAME
                // field.
                txtBoxBinding = new Binding("Text", bindingSource1, "OBJECTID");
                textBox1.DataBindings.Add(txtBoxBinding);

                //Bind Source to cbo?
                IFields fields = m_iFeatureClass.Fields;
                IField field = null;
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    // Get the field at the given index.
                    field = fields.get_Field(i);
                    if (field.Name != field.AliasName)
                    {
                        ComboboxItem item = new ComboboxItem();
                        item.Text = field.AliasName;
                        item.Value = field.Name;

                        cboFields.Items.Add(item);

                    }
                }
                //cboFields.DataBindings.Add(txtBoxBinding);
            }
        }
        #endregion

        public class ComboboxItem
        {
            public string Text { get; set; }
            public object Value { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }


        private IFeatureClass getFeatureClass()
        {
            //feature class that contains meters
            IFeatureClass iFeatureClass = null;
            try
            {

                //get the focus map
                IMap iMap = axMapControl1.ActiveView.FocusMap;
                //Loop through the layers and get specifically "wMeter"

                //Feature layer UID
                UID uID = new UID();
                uID.Value = "{40A9E885-5533-11D0-98BE-00805F7CED21}";
                //get layers of type Ifeature layer
                IEnumLayer iEnumLayer = iMap.get_Layers(uID, true);
                iEnumLayer.Reset();
                ILayer iLayer = iEnumLayer.Next();


                while (iLayer != null)
                {
                    IFeatureLayer iFeatureLayer = (IFeatureLayer)iLayer;
                    m_iFeatureLayer = iFeatureLayer;
                    string tablename = ((IDataset)iFeatureLayer.FeatureClass).BrowseName;
                    if (tablename.Equals("wMeter"))
                    {
                        iFeatureClass = ((IFeatureLayer)iLayer).FeatureClass;
                        //exit while loop
                        break;
                    }
                    //move to the next  layer
                    iLayer = iEnumLayer.Next();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());

            }
            return iFeatureClass;

        }

        private void btnGrid_Click(object sender, EventArgs e)
        {
            try
            {
                bindTableToGrid();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridView1.Rows[e.RowIndex].Selected = true;
            if (m_iFeatureClass == null) { m_iFeatureClass = getFeatureClass(); }


        }

        private void dataGridView1_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            
        }
    }
}