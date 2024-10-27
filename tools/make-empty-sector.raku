
#
#  Generates an empty .tab sector, and its metadata .xml file.
#

sub MAIN(Str $sector-name) {
	my $abbrev = $sector-name.substr(0,4).lc.tc;

	# generate file content
	my $metafile-content = generate-metafile($sector-name, $abbrev);
	my $empty-sector-content = generate-empty-sector();

	# write files
	spurt "$abbrev.xml", $metafile-content;
	spurt "$abbrev.tab", $empty-sector-content;

	say "Empty '$sector-name' sector ($abbrev.tab and $abbrev.xml) created!";
}

sub generate-metafile($name, $abbr) {
	return qq:to/ENDOFMETAFILE/;
<?xml version="1.0"?>
<Sector Abbreviation="$abbr" Tabs="Unreviewed">
   <Name>{$name}</Name>
   <Credits>
		Empty sector $name created by Raku Script, October 2024.
		This is an empty sector file template for use with Traveller Map.
   </Credits>
   <Subsectors>
{  generate-subsectors()  }
   </Subsectors>
</Sector>
ENDOFMETAFILE
}

sub generate-subsectors() {
	return ('A'..'P').map({"      <Subsector Index=\"$_\"></Subsector>"}).join("\n");
}

sub generate-empty-sector() {
	return "Sector	SS	Hex	Name	UWP	Bases	Remarks	Zone	PBG	Allegiance	Stars	\{Ix\}	\(Ex\)	\[Cx\]	Nobility	W	RU\n";
}
