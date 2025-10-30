from PIL import Image
import argparse
import os
from tqdm import tqdm

parser = argparse.ArgumentParser()

parser.add_argument("--input_dir")
parser.add_argument("--output_dir")

args = parser.parse_args()

os.makedirs(args.output_dir,exist_ok=True)


for file in tqdm(os.listdir(args.input_dir)):
    img = Image.open(os.path.join(args.input_dir,file))
    img = img.resize((640,480))
    img.save(os.path.join(args.output_dir,file))
