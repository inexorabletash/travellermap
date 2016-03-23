<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output method="html"/>

<xsl:template match="/">
    <html>
        <head>
            <title>Sector Credits</title>
            <style>
                body, table { font-size: 8pt; font-family: sans-serif; }
                th, td { white-space: nowrap; text-align: left; vertical-align: top; }
                th:hover { text-decoration: underline; cursor: pointer; }
            </style>
            <script src="https://cdn.rawgit.com/inexorabletash/polyfill/v0.1.18/polyfill.min.js"></script>
            <script src="credits.js"></script>
        </head>
        <body>
            <p>
                Hi there!
            </p>
            <p>
                If you are reading this, you're seeing the data file that drives the whole site filtered through
                a "credits checker" that the site creator uses to review the attribution and other metadata
                associated with files.
            </p>
            <p>
                To view the raw XML data, right-click on the page and select "View Source"
            </p>
            <hr />
            <table>
                <tbody>
                    <tr>
                        <th>X</th>
                        <th>Y</th>
                        <th>Name</th>
                        <th>Type</th>
                        <th>DataFile</th>
                        <th># SS</th>
                        <th>Author</th>
                        <th>Source</th>
                        <th>Publisher</th>
                        <th>Copyright</th>
                        <th>Era</th>
                        <th>Ref</th>
                    </tr>
                    <xsl:apply-templates select="Sectors/Sector">
                        <xsl:sort select="Y" data-type="number"/>
                        <xsl:sort select="X" data-type="number"/>
                    </xsl:apply-templates>
                </tbody>
            </table>
        </body>
    </html>
</xsl:template>

<xsl:template match="Sector">
    <tr>
        <xsl:choose>
            <xsl:when test="DataFile/@Ref">
                <xsl:attribute name="style">background-color: #ffffb0;</xsl:attribute>
            </xsl:when>
            <xsl:when test="DataFile">
                <xsl:attribute name="style">background-color: #ffb0b0;</xsl:attribute>
            </xsl:when>
        </xsl:choose>
        <td>
            <xsl:value-of select="X"/>
        </td>
        <td>
            <xsl:value-of select="Y"/>
        </td>
        <td>
            <xsl:value-of select="Name[1]"/>
        </td>
        <td>
            <xsl:value-of select="DataFile/@Type"/>
        </td>
        <td>
            <xsl:value-of select="DataFile"/>
        </td>
        <td>
            <xsl:value-of select="count( Subsectors/Subsector )"/>
        </td>
        <td>
            <xsl:value-of select="DataFile/@Author"/>
        </td>
        <td>
            <xsl:value-of select="DataFile/@Source"/>
        </td>
        <td>
            <xsl:value-of select="DataFile/@Publisher"/>
        </td>
        <td>
            <xsl:value-of select="DataFile/@Copyright"/>
        </td>
        <td>
            <xsl:value-of select="DataFile/@Era"/>
        </td>
        <td>
            <a>
                <xsl:attribute name="href">
                    <xsl:value-of select="DataFile/@Ref"/>
                </xsl:attribute>
                <xsl:value-of select="DataFile/@Ref"/>
            </a>
        </td>
    </tr>

</xsl:template>


</xsl:stylesheet>
