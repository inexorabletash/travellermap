
curl -o regina.png https://travellermap.com/data/spin/1910/image?clip=0&rea=0.15&accept=application/pdf

Save as PNG using Preview

for i in 57 72 76 114 120 128 144 152 180 192 512; do
    convert regina.png -resize "${i}x${i}^" -gravity center -crop "${i}x${i}+0+0" regina${i}.png
done
