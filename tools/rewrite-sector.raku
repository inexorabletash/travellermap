#
#  Fills out missing pieces of a sector.  BUT:
#
#  - Stellar data is always generated, and only the primary is generated (currently).
#  - Remarks always get regenerated, and the script doesn't preserve special remarks (currently).
#  - Ix, Ex, Cx and RU are always generated.
#  - Nobility is NOT currently generated. 
#
#  The intent of this script is for generating a new sector, but to detect and
#  preserve existing data if there is any.
#
#  Here's how it works: every field that is a question mark gets determined,
#  except for the UWP and PBG.  The UWP has to be ???????-? to get generated.
#  The PBG has to be ??? to get generated.
#
my %ehex = (0...34) Z=> (0...9, 'A'...'H', 'J'...'N', 'P'...'Z');
my %dehex = (0...9, 'A'...'H', 'J'...'N', 'P'...'Z', '-') Z=> (0...34, 0);

sub MAIN( Str $sector ) { # e.g. "Dushis"
	my $file = "$sector.tab";
	
	my $first = 1;
	for $file.IO.lines -> $line {

	    if ($first) {
			$first = 0;
			say $line;
			next;
		}

		my @tabs = $line.split(/\t/);

	    @tabs[3] = get-name() if @tabs[3] eq '?';
		@tabs[4] = generateUWP() if @tabs[4] eq '???????-?';

		my ($sp,$siz,$atm,$hyd,$pop,$gov,$law,$dash,$tl) = decodeUWP(@tabs[4]); # fetch

		@tabs[5] = roll-bases( $sp ) if @tabs[5] eq '?';
		@tabs[6] = get-all-remarks(@tabs[4]).sort().join(' ').trim();
		# @tabs[7] = zone
		@tabs[8] = roll-pbg($pop) if @tabs[8] eq '???';
		my @pbg = @tabs[8].comb;
		# @tabs[9] = allegiance
		@tabs[10] = join ' ', generateStellarData(); # 'M0 V';    
		my $ix = calc-importance(@tabs[4], @tabs[6], @tabs[5]); 
		@tabs[11] = '{ ' ~ $ix ~ ' }';
		(@tabs[12], @tabs[16]) = calc-economic-extension-and-ru($pop, $tl, @pbg[2], @pbg[1], $ix);
		@tabs[12] = '(' ~ @tabs[12] ~ ')';
		@tabs[13] = '[' ~ roll-hass( $pop, $tl, $ix ) ~ ']';  # [Cx]
		@tabs[14] = '';        # Nobility
		@tabs[15] = roll-worlds(@pbg[2], @pbg[1]) if @tabs[15] eq '?';

		say @tabs.join("\t");
	}
}

sub get-name {
	my $proc = run 'perl', 'name.pl', :out;
	return $proc.out.slurp(:close).trim();
}

sub flux() {
	return (^6).pick - (^6).pick;
}

sub decodeUWP(Str $uwp) {
	my @uwp = $uwp.comb;
    my @r = @uwp.shift;    # don't demap the starport
	for @uwp -> $elem {
		push @r, %dehex{ $elem };
	}
	return @r;
}
sub is-garden-world(Str $str) {
	my ($sp, $siz, $atm, $hyd) = decodeUWP($str);

	return True if 
			(6 <= $siz <= 8) &&
			($atm == 5 || $atm == 6 || $atm == 8) &&
			(5 <= $hyd <= 7);
}

sub get-all-remarks(Str $str) {
		my @r;
		my ($sp,$siz,$atm,$hyd,$pop,$gov,$law,$dash,$tl) = decodeUWP($str);

		push @r, 'As' if $str ~~ /^.000/;
		push @r, 'De' if $str ~~ /^..<[2..9]>0/;
		push @r, 'Fl' if $str ~~ /^..<[ABC]><[1..9A]>/;
		push @r, 'Ga' if is-garden-world($str);
		push @r, 'He' if $str ~~ /^.<[3..9ABC]><[2479ABC]><[012]>/;
		push @r, 'Ic' if $str ~~ /^..<[01]><[1..9A]>/;
		push @r, 'Oc' if $str ~~ /^.<[A..F]><[3..9DEF]>A/;
		push @r, 'Va' if $str ~~ /^..0/;
		push @r, 'Wa' if $str ~~ /^.<[3..9]><[3..9DEF]>A/;

        push @r, 'Cy' if $str ~~ /^....<[5..9A]>6<[0123]>/; # COLONY ADDED HERE.
		push @r, 'Di' if $pop == 0 && $tl > 0;
		push @r, 'Ba' if $pop == 0 && $tl == 0;
		push @r, 'Lo' if 0 < $pop <= 3;
		push @r, 'Ni' if 4 <= $pop <= 6;
		push @r, 'Ph' if $pop == 8;
		push @r, 'Hi' if $pop >= 9;

		if 4 <= $atm <= 9 && 4 <= $hyd <= 8 {
			push @r, 'Pa' if $pop == 4 || $pop == 8;
			push @r, 'Ag' if 5 <= $pop <= 7;
		}

		push @r, 'Na' if $str ~~ /^..<[0123]><[0123]><[6789A..F]>/;
		push @r, 'Px' if $str ~~ /^..<[23AB]><[1..5]><[3456]>.<[6789]>/;
		push @r, 'Pi' if $str ~~ /^..<[012479]>.<[78]>/;
		push @r, 'In' if $str ~~ /^..<[012479ABC]>.<[9A..F]>/;
		push @r, 'Po' if $str ~~ /^..<[2345]><[0123]>/;

		if $atm == 6 || $atm == 8 {
			push @r, 'Pr' if $pop == 5 || $pop == 9;
			push @r, 'Ri' if 6 <= $pop <= 8;
		}

		push @r, qw/Mr Re Px/.pick if $gov == 6;

		return @r;
	}

sub generateStellarData() 
{
	my @stars = (rollStar());
	#my $companion = (flux() > 2) ?? rollStar( (^6).pick ) !! '';
	#my $close     = (flux() > 2) ?? rollStar( (^6).pick ) !! ''; # , (^6).pick ) !! '';
	#my $near      = (flux() > 2) ?? rollStar( (^6).pick ) !! ''; # , (^6).pick + 6) !! '';
	#my $far       = (flux() > 2) ?? rollStar( (^6).pick ) !! ''; # , (^6).pick + 12) !! '';

	#@stars.push(rollStar((^6).pick)) if flux() > 2;
	#@stars.push(rollStar((^6).pick)) if flux() > 2;
	#@stars.push(rollStar((^6).pick)) if flux() > 2;
	#@stars.push(rollStar((^6).pick)) if flux() > 2;
    return @stars;
}

sub rollStar(Int $dm = 0, Int $orbit = -1)
{
	my $roll = (^6).pick + (^6).pick + $dm;
	$roll = 13 if $roll > 13;
	my $type = qw/A A F F G G K K M M M BD BD BD/[$roll];

	my $subroll = (^6).pick + (^6).pick;
	my $subtype;

	my $decimal = (^10).pick;

	if ($type eq 'A') {
		$subtype = qw/Ia Ib II III IV V V V V V V/[$subroll];
	}
	elsif ($type ~~ /<[FGK]>/) {
		$subtype = qw/II III IV V V V V V V V VI/[$subroll];
	}
	else { # 'M'
		$subtype = qw/II II II III V V V V V VI D D/[$subroll];
	}

    my $prefix = '';
	$prefix = " $orbit:" if $orbit > -1;
    return "$prefix$type"                   if $type eq 'BD';
	return "$prefix$type$decimal $subtype"; # otherwise
}

sub roll-pbg(Int $pop)
{
	my $p = 0;
	$p = (^9).pick + 1 if $pop > 0;

	my $b = (^6).pick - 2;
	$b = 0 if $b < 0;

	my $g = (((^6).pick + (^6).pick)/2).Int;

    return $p ~  $b ~ $g;
}

sub generateUWP()
{
	my $sp  = ('A','A','A','B','B','C','C','D','E','E','X')[(^11).pick];

	my $siz = (^6).pick + (^6).pick;
	$siz = 9 + (^6).pick if $siz == 10;

	my $atm = (^6).pick - (^6).pick + $siz;
	$atm = 0 if $atm < 0 or $siz == 0;
	$atm = 15 if $atm > 15;

	my $hyd = (^6).pick - (^6).pick + $atm;
	$hyd -= 4 if $atm < 2 or $atm > 9;
	$hyd = 0  if $hyd < 0 or $siz < 2;
	$hyd = 10 if $hyd > 10;

	my $pop = (^6).pick + (^6).pick;
	$pop = (^6).pick + (^6).pick + 5 if $pop == 10;

	my $gov = (^6).pick - (^6).pick + $pop;
	$gov = 0 if $gov < 0 or $pop == 0;
	$gov = 15 if $gov > 15;

	my $law = (^6).pick - (^6).pick + $gov;
	$law = 0 if $law < 0 or $pop == 0;
	$law = 18 if $law > 18;

	my $tl  = (^6).pick + (^6).pick;
	$tl += 7 if $sp eq 'A';
	$tl += 5 if $sp eq 'B';
	$tl += 3 if $sp eq 'C';
	$tl -= 4 if $sp eq 'X';
	$tl += 2 if $siz < 2;
	$tl += 1 if 1 < $siz < 5;
	$tl += 1 if $siz > 9; # why not
	$tl += 1 if $atm < 4;
	$tl += 1 if $atm > 9;
	$tl += 1 if $atm == 4 or $atm == 7 or $atm == 9;
	$tl += 2 if $hyd == 9;
	$tl += 3 if $hyd == 10;
	$tl += 1 if $pop < 6;
	$tl += 4 if $pop > 8; 
	$tl += 1 if $gov == 0 or $gov == 5;
	$tl -= 2 if $gov == 13;
	$tl = 0 if $tl < 0 or $pop == 0;

	return sprintf("%s%s%s%s%s%s%s-%s", 
		$sp, 
		%ehex{$siz},
		%ehex{$atm},
		%ehex{$hyd},
		%ehex{$pop},
		%ehex{$gov},
		%ehex{$law},
		%ehex{$tl});
}

sub roll-bases(Str $sp)
{
	return '' if $sp ~~ /<[EX]>/;

    my $bases = '';
	my $navyRoll  = (^6).pick + (^6).pick + 2;

	$bases = ($navyRoll > 5 ?? '' !! 'N') if $sp eq 'B';
	$bases = ($navyRoll > 6 ?? '' !! 'N') if $sp eq 'A';

	my $scoutRoll = (^6).pick + (^6).pick + 2;

    $bases ~= ($scoutRoll > 7 ?? '' !! 'S') if $sp eq 'D';
	$bases ~= ($scoutRoll > 6 ?? '' !! 'S') if $sp eq 'C';
	$bases ~= ($scoutRoll > 5 ?? '' !! 'S') if $sp eq 'B';
	$bases ~= ($scoutRoll > 4 ?? '' !! 'S') if $sp eq 'A';

	return $bases;
}

sub calc-importance(Str $uwp, Str $remarks, Str $bases) 
{
	my ($sp,$siz,$atm,$hyd,$pop,$gov,$law,$dash,$tl) = decodeUWP($uwp);
	#say "$sp $siz $atm $hyd $pop $gov $law $dash $tl";
	my $ix = 0;
	++$ix if $sp ~~ /<[AB]>/;
	--$ix if $sp ~~ /<[DEX]>/;
	++$ix if $tl > 15; # stacks!
	++$ix if $tl > 9;
	--$ix if $tl < 9;

	++$ix if $remarks ~~ /Ag/;
	++$ix if $remarks ~~ /In/;
	++$ix if $remarks ~~ /Ri/;
	++$ix if $remarks ~~ /Hi/;

	--$ix if $pop < 7;
	++$ix if $bases ~~ /NS/; # Naval and Scout base both present
	++$ix if $bases ~~ /W/; 

	return $ix;
}

sub calc-economic-extension-and-ru($pop, $tl, $ggs, $belts, $ix) 
{
	#say "ex: pop $pop, tl $tl, gg $ggs, belt $belts, ix $ix";
	my @rlie = qw/0 0 0 0/;
	@rlie[0] = (^6).pick + (^6).pick + 2;
	@rlie[0] += $ggs + $belts  if $tl > 7;

	@rlie[1] = $pop - 1 if $pop > 1;

	@rlie[2] = $ix            if $pop > 0;  # 0..3
	@rlie[2] += (^6).pick + 1 if $pop > 3;  # 4..6
	@rlie[2] += (^6).pick + 1 if $pop > 6;  # 7+
	@rlie[2] = 0 if @rlie[2] < 0;

	@rlie[3] = (^6).pick - (^6).pick;
	@rlie[3] = 1 if @rlie[3] == 0;

	#
	#  Resource Units
	#
	my $ru = @rlie[0] || 1;
	$ru *= @rlie[1] if @rlie[1] > 0;
	$ru *= @rlie[2] if @rlie[2] > 0;
	$ru *= @rlie[3];

	#
	#  Now package up Ex
	#
	@rlie[0] = %ehex{ @rlie[0] };
	@rlie[1] = %ehex{ @rlie[1] };
	@rlie[2] = %ehex{ @rlie[2] };

	my $rlie = @rlie[0] ~ @rlie[1] ~ @rlie[2];
	$rlie ~= '+' if @rlie[3] > -1;
	$rlie ~= '-' if @rlie[3] < 0;
	$rlie ~= %ehex{ @rlie[3].abs };

	return $rlie, $ru;
}

sub roll-worlds( $ggs, $belts )
{
	return 1 + $ggs + $belts + (^6).pick + (^6).pick + 2;
}

sub roll-hass( $pop, $tl, $ix )
{
	return '0000' if $pop == 0;

	my $h  = $pop + flux();
	$h = 1 if $h < 1;
	my $a  = $pop + $ix;
	$a = 1 if $a < 1;
	my $s1 = 5 + flux();
	$s1 = 1 if $s1 < 1;
	my $s2 = $tl + flux();
	$s2 = 1 if $s2 < 1;

    my $out = %ehex{ $h };
	$out ~= %ehex{ $a };
	$out ~= %ehex{ $s1 };
	$out ~= %ehex{ $s2 };

	#note "pop $pop, tl $tl, ix $ix => Symbols $s2\n";
	return $out;
}