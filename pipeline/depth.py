import numpy as np
from PIL import Image
import torch
import cv2
import matplotlib.pyplot as plt
import argparse
import os
from tqdm import tqdm
import tifffile
import sys

sys.path.append("/home/sotka/dev/UniDepth")

from unidepth.models import UniDepthV2

type_ = "l"  # available types: s, b, l
name = f"unidepth-v2-vit{type_}14"
model = UniDepthV2.from_pretrained(f"lpiccinelli/{name}") # or "lpiccinelli/unidepth-v1-cnvnxtl" for the ConvNext backbone
# Move to CUDA, if any
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
model = model.to(device)

if __name__ == "__main__":

    parser = argparse.ArgumentParser()

    parser.add_argument("--input_dir")
    parser.add_argument("--output_dir")

    args = parser.parse_args()

    os.makedirs(args.output_dir,exist_ok=True)

    files = os.listdir(args.input_dir)

    for file in tqdm(files):
        idx = file.split("_")[0]
        # Load the RGB image and the normalization will be taken care of by the model
        rgb = torch.from_numpy(np.array(Image.open(os.path.join(args.input_dir,file)))).permute(2, 0, 1) # C, H, W
        if rgb.shape[0]==4:
            rgb = rgb[:3]

        depth = model.infer(rgb)['depth'].cpu().numpy()[0,0]

        # Save as TIFF with no compression
        tifffile.imwrite(os.path.join(args.output_dir,f"{idx}_depth.tiff"), depth, compression=None, photometric=1,bitspersample=32,description=None)

