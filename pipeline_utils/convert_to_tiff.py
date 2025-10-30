import OpenEXR
import numpy as np
import cv2
import os
from tqdm import tqdm
import argparse
import tifffile

def read_exr(file_path):
    """
    Read an EXR file and return it as a numpy array
    """
    # Open the EXR file
    exr_file = OpenEXR.File(file_path)

    img = exr_file.parts[0].channels['Y'].pixels

    img = cv2.resize(img,(640,480))

    return img

def exr_to_tiff(input_path, output_path, bits_per_sample=32):
    """
    Convert EXR file to TIFF format
    
    Parameters:
    - input_path: path to input EXR file
    - output_path: path to save TIFF file
    - bits_per_sample: 32 for float32, 16 for float16
    """
    # Read the EXR file
    img = read_exr(input_path)

    # Save as TIFF with no compression
    tifffile.imwrite(output_path, img, compression=None, photometric=1,bitspersample=32,description=None)

if __name__ == "__main__":
    # Example usage
    parser = argparse.ArgumentParser()

    parser.add_argument("--input_dir")
    parser.add_argument("--output_dir")

    args = parser.parse_args()

    os.makedirs(args.output_dir,exist_ok=True)

    for file in tqdm(os.listdir(args.input_dir)):
        input_file = os.path.join(args.input_dir,file)
        idx = file.split("_")[1]
        output_file = os.path.join(args.output_dir,f"{idx}_depth.tiff")

        exr_to_tiff(input_file, output_file)