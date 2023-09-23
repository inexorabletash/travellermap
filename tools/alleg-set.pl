print STDERR<<EOINTRO;
******************************************************************************
  _______ __ __             __                               
 |   _   |  |  .-----.-----|__.---.-.-----.----.-----.       
 |.  1   |  |  |  -__|  _  |  |  _  |     |  __|  -__|       
 |.  _   |__|__|_____|___  |__|___._|__|__|____|_____|       
 |:  |   |           |_____|                                 
 |::.|:. |                                                   
 `--- ---'                                                   
  _______       __   __                
 |   _   .-----|  |_|  |_.-----.----.  
 |   1___|  -__|   _|   _|  -__|   _|  
 |____   |_____|____|____|_____|__|    
 |:  1   |                             
 |::.. . |                             
 `-------'                             
 
******************************************************************************
EOINTRO
use strict;

my $sector    = shift or die "\nNEED SECTOR!\n\nSYNOPSIS: $0  >SECTOR.tab<  ALLEG  hex...\n\n";
$sector      .= '.tab' unless $sector =~ /\.tab$/;
my $newAlleg  = shift; # or die "\nNEED ALLEGIANCE!\n\nSYNOPSIS: $0  $sector  >ALLEG<  hex...\n\n";
my @hexes     = @ARGV; 

my %allegInventory = ();

open my $in, '<', $sector or die "ERROR: cannot read sector file $sector\n";

#
#  The header always contains these fields in order:
#
#  Sector, SS, Hex, Name, UWP, Bases, Remarks, Zone, PBG, Allegiance, Stars, {Ix}, (Ex), [Cx], Nobility, W, RU
#
my $hdr = <$in>; # read header line
my @hdr = split /\t/, $hdr;
print $hdr if $newAlleg ne '';

my @lines = <$in>;
close $in;

foreach my $line (@lines)
{
    my ($sector, $ss, $hex, $name, $uwp, $bases, $remarks, $zone, $pbg, $allegiance, $stars, $ix, $ex, $cx, $nobility, $w, $ru) = split /\t/, $line;

    if ($newAlleg ne '')
    {
       if (grep( /$hex/, @hexes))
       {
           print STDERR "updated $hex from $allegiance to $newAlleg\n";
           $allegiance = $newAlleg;
       }
    }

    $allegInventory{ $allegiance }++;

    if ($newAlleg ne '')
    {
       print join "\t", $sector, $ss, $hex, $name, $uwp, $bases, $remarks, $zone, $pbg, $allegiance, $stars, $ix, $ex, $cx, $nobility, $w, $ru
          if @hexes;
    }
}

print STDERR "\n\nAllegiance Inventory:\n";
foreach my $alleg (sort keys %allegInventory)
{
   print STDERR sprintf "   %4s : $allegInventory{$alleg}\n", $alleg;
}
print STDERR "\n";
