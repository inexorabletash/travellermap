sub trim ($) {
    my ($s) = @_;
    $s =~ s/^\s+//;
    $s =~ s/\s+$//;
    return $s;
}

sub trimEnd($) {
    my ($s) = @_;
    $s =~ s/\s+$//;
    return $s;
}

sub quote($) {
    my ($s) = @_;
    $s =~ s/["\\]/\\$1/g;
    return '"' . $s . '"';
}

sub parseColumnDelimited($) {
    my ($text) = @_;
    my @lines = split("\n", $text);
    my $header = shift @lines;
    my $separator = shift @lines;
    my @columns;
    my @offsets;
    my @widths;
    my $offset = 0;
    while ($separator =~ /((.)\2*)/g) {
        my $span = $1;
        if ($span =~ m/^-+$/) {
            my $width = length($span);
            push @columns, trimEnd(substr($header, $offset, $width));
            push @offsets, $offset;
            push @widths, $width;
        }
        $offset += length($span);
    }
    my @worlds;
    foreach my $line (@lines) {
        my $world = {};
        for (my $i = 0; $i <= $#columns; ++$i) {
            $world->{$columns[$i]} = trimEnd(substr($line, $offsets[$i], $widths[$i]));
        }
        push @worlds, $world;
    }

    return @worlds;
}

1;
