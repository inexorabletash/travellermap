foreach my $file (<*.*>)
{
   open IN, $file;
   my @lines = <IN>;
   close IN;

   foreach my $line (@lines)
   {
      $line =~ s/\xA0/ /g;
      print $line;
   }
}
