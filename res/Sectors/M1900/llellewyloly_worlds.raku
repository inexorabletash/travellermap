
#
#  This script combs over three sectors, looking for
#  "Llellewyloly Prime" habitable worlds.
#

#
#  Note that each sector has specific filters, including
#  subsector to search, but also the environmental parameters
#  are slightly varied. I reasoned that Spinward worlds are
#  the most desirable location, followed by Gvurrdon and 
#  then Tuglikki last. 
#

for "Spin.tab".IO.lines -> $line {
	my ($sec, $ss, $hex, $name, $uwp) = $line.split(/\t/);
    
    next unless $ss ~~ /<[BCDEFGHIJKLNOP]>/;
	next unless $uwp ~~ /^.<[4]><[35]><[345]>/ or $uwp ~~ /^.53<[345]>/;

	say 'SPIN ' ~ $hex ~ $name.fmt("  %-15s ") ~ $uwp;
}


for "Gvurrdon.tab".IO.lines -> $line {
	my ($sec, $ss, $hex, $name, $uwp) = $line.split(/\t/);
    
    next unless $ss ~~ /<[KLNOP]>/;
	next unless $uwp ~~ /^.<[4]><[35]><[234]>/;

	say 'GVUR ' ~ $hex ~ $name.fmt("  %-15s ") ~ $uwp;
}

for "Tuglikki.tab".IO.lines -> $line {
	my ($sec, $ss, $hex, $name, $uwp) = $line.split(/\t/);
    
    next unless $ss ~~ /<[IMN]>/;
	next unless $uwp ~~ /^.<[4]><[35]><[34]>/;

	say 'TUGL ' ~ $hex ~ $name.fmt("  %-15s ") ~ $uwp;
}