# Some basic setup:
# Setup detectron2 logger
from detectron2.utils.logger import setup_logger
import json
from tqdm import tqdm
import argparse
setup_logger()

# import some common libraries
import os, json, cv2
import sys

# import some common detectron2 utilities
from detectron2 import model_zoo
from detectron2.engine import DefaultPredictor
from detectron2.config import get_cfg

cfg = get_cfg()
cfg.merge_from_file(model_zoo.get_config_file("COCO-PanopticSegmentation/panoptic_fpn_R_101_3x.yaml"))
#cfg.MODEL.ROI_HEADS.SCORE_THRESH_TEST = 0.5  # set threshold for this model
cfg.MODEL.WEIGHTS = model_zoo.get_checkpoint_url("COCO-PanopticSegmentation/panoptic_fpn_R_101_3x.yaml")
predictor = DefaultPredictor(cfg)

if __name__ == "__main__":

    parser = argparse.ArgumentParser()

    parser.add_argument("--input_dir")
    parser.add_argument("--output_dir")

    args = parser.parse_args()
    
    os.makedirs(args.output_dir,exist_ok=True)

    files = os.listdir(args.input_dir)

    for file in tqdm(files):
        idx = file.split("_")[0]

        img_path = os.path.join(args.input_dir,file)
        im = cv2.imread(img_path)

        panoptic_seg, segments_info = predictor(im)["panoptic_seg"]

        with open(os.path.join(args.output_dir,f"{idx}_labels.json"),"w") as f:
            json.dump(segments_info,f)

        panoptic_seg = panoptic_seg.cpu().numpy()
        cv2.imwrite(os.path.join(args.output_dir,f'{idx}_predicted.png'),panoptic_seg)
