#!/usr/bin/env perl

# Instructions:
# * Go to Traveller World Details sheet:
#   https://docs.google.com/spreadsheets/d/1wFJ7SROI1kk9oY3KFB-nXr1Ek9G2RDxiYNdkJCOTO88/edit#gid=0
# * Select All
# * In a terminal: pbpaste | ./make_json.pl > world_details.json

use strict;
use warnings;

sub trim ($) {
    my ($s) = @_;
    $s =~ s/^\s+//;
    $s =~ s/\s+$//;
    return $s;
}
sub enquote ($) {
    my ($s) = @_;
    $s =~ s/([\\"])/\\$1/g;
    return '"'.$s.'"';
}

my $header = <STDIN>;
my @header = map { trim($_) } split('\t', $header);
my @parsed = ();
foreach my $line (<STDIN>) {
    # Parse tab-delimited lines into fields
    chomp $line;
    next unless trim($line);
    my @cols = map { trim($_) } split("\t", $line);
    my %fields = ();
    for my $i (0..$#header) {
        my $value = $cols[$i] || "";
        $value = $1 if $value =~ /^"(.*)"$/;
        $fields{$header[$i]} = $value;
    }
    push @parsed, \%fields;
}

my @out = ();
foreach my $record (@parsed) {
    next unless $record->{'TM'} eq 'Y';
    my $key = $record->{'Sector'} . ' ' . $record->{'Hex'};
    my $value = $record->{'Source'};
    push @out, enquote($key).':'.enquote($value);
}
print '{' . join(',', @out) . "}\n";
