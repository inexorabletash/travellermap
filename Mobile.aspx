<%@ Page language="c#" Codebehind="Mobile.aspx.cs" AutoEventWireup="false" Inherits="Maps.Pages.MobilePage" EnableViewStateMac="false" codePage="65001"%>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN" >
<html xmlns="http://www.w3.org/1999/xhtml">
	<head>
		<title>Traveller Map</title>
		<meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
		<meta content="Microsoft Visual Studio .NET 7.1" name="GENERATOR" />
		<meta content="C#" name="CODE_LANGUAGE" />
		<meta content="JavaScript" name="vs_defaultClientScript" />
		
		
		<!-- iPhone specific -->
		<meta name="viewport" content="width = 320, initial-scale = 1.0, user-scalable = no" />
		<meta name="apple-mobile-web-app-capable" content="yes" />
		<meta name="apple-mobile-web-app-status-bar-style" content="black" />
        <!--
		<script type="text/javascript">
		    if ("ontouchstart" in window) {
	            if (window.confirm('Switch to the experimental "touch" version?')) {
	                window.location = 'touch';
	            }
		    }
		</script>
		-->
		<script type="text/javascript">
            window.onload = function() {
                window.setTimeout( function() { window.scrollTo(0,1) }, 100 );
            };
		</script>
	</head>
	<body style="background-color: #000000;">
		<div style="text-align: center;">
			<form id="Form1" method="post" runat="server">
				<p>
					<asp:ImageMap id="MapImage" runat="server" forecolor="White" height="128px" backcolor="Black"
						width="128px" enableviewstate="False" HotSpotMode="PostBack" OnClick="MapImage_Click">
						<asp:RectangleHotSpot AlternateText="Spin-Coreward" PostBackValue="0" />
						<asp:RectangleHotSpot AlternateText="Coreward" PostBackValue="1" />
						<asp:RectangleHotSpot AlternateText="Core-Trailing" PostBackValue="2" />
						<asp:RectangleHotSpot AlternateText="Spinward" PostBackValue="3" />
						<asp:RectangleHotSpot AlternateText="Zoom In" PostBackValue="4" />
						<asp:RectangleHotSpot AlternateText="Trailing" PostBackValue="5" />
						<asp:RectangleHotSpot AlternateText="Spin-Rimward" PostBackValue="6" />
						<asp:RectangleHotSpot AlternateText="Rimward" PostBackValue="7" />
						<asp:RectangleHotSpot AlternateText="Rim-Trailing" PostBackValue="8" />
					</asp:ImageMap>
				</p>
				<p style="display: none;">
					<asp:button id="ButtonScrollCoreward" runat="server" text="Coreward" enableviewstate="False" ToolTip="Scroll Coreward"></asp:button><br />
					<asp:button id="ButtonScrollSpinward" runat="server" text="Spinward" enableviewstate="False" ToolTip="Scroll Spinward"></asp:button><asp:button id="ButtonScrollTrailing" runat="server" text="Trailing" enableviewstate="False" ToolTip="Scroll Trailing"></asp:button><br />
					<asp:button id="ButtonScrollRimward" runat="server" text="Rimward" enableviewstate="False" ToolTip="Scroll Rimward"></asp:button>
				</p>
				<p>
					<asp:button id="ButtonZoomIn" runat="server" text="Zoom In" enableviewstate="False" ToolTip="Zoom In"></asp:button>
					<asp:button id="ButtonZoomOut" runat="server" text="Zoom Out" enableviewstate="False" ToolTip="Zoom Out"></asp:button>
				</p>
				<hr style="display: none; width: 192px;" />
				<p>
					<asp:TextBox id="TextBoxSearch" runat="server" ToolTip="Enter search term, then click Search or Jump!"></asp:TextBox>
					<asp:Button id="ButtonSearch" runat="server" text="Search" ToolTip="Search"></asp:Button>
					<asp:Button id="ButtonJump" runat="server" text="Jump!" ToolTip="Jump to Nearest Match"></asp:Button>
				</p>
				<p>
					<asp:DataList id="ResultsDataList" runat="server">
						<ItemTemplate>
							<p>
								<asp:LinkButton id="SelectButton" text='<%# DataBinder.Eval(Container, "DataItem.Name") %>' commandname="Select" commandargument='<%# DataBinder.Eval(Container, "DataItem.Data") %>' runat="server"/>
								<br>
								<small>(<%# DataBinder.Eval(Container, "DataItem.Details") %>)</small>
							</p>
						</ItemTemplate>
					</asp:DataList>
					<asp:Label id="LabelNoResults" runat="server" visible="False" font-italic="True">No matches found</asp:Label>
				</p>
				<hr style="width: 192px;" />
				<p>
                    <asp:DropDownList ID="DropDownTileSize" runat="server" AutoPostBack="True" OnSelectedIndexChanged="DropDownTileSize_SelectedIndexChanged">
                        <asp:ListItem Selected="True" Value="128">Small (128x128)</asp:ListItem>
                        <asp:ListItem Value="192">Medium (192x192)</asp:ListItem>
                        <asp:ListItem Value="256">Large (256x256)</asp:ListItem>
                        <asp:ListItem Value="384">Huge (384x384)</asp:ListItem>
                    </asp:DropDownList>
				</p>
			</form>
		</div>
	</body>
</html>
