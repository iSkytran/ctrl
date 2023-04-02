from gymnasium.spaces import Box, MultiBinary
from numpy.typing import NDArray
from skimage.transform import resize
from skimage.util import img_as_ubyte
import gymnasium as gym
import json
import mss
import numpy as np
import time
import zmq


class CtrlEnv(gym.Env):
    metadata = {"render_modes": ["rgb_array"]}

    def __init__(self, render_mode=None, monitor_num: int = 1) -> None:
        # Define spaces. 
        self.observation_space = Box(
            low=0, high=255, shape=(144, 256, 3), dtype=np.uint8
        )
        self.action_space = MultiBinary(7)

        # Rendering variables.
        self.frame = None
        assert render_mode is None or render_mode in self.metadata["render_modes"]
        self.render_mode = render_mode

        # Screenshot utils to get observations.
        self.sct = mss.mss()
        self.monitor = self.sct.monitors[monitor_num]


        # Communication to C# component.
        self.context = zmq.Context()
        self.socket = self.context.socket(zmq.REQ)
        self.socket.connect("tcp://localhost:7777")

        # Give user time to focus game window.
        time.sleep(5)

    def _get_obs(self) -> NDArray:
        obs = np.array(self.sct.grab(self.monitor))  # Get observation as numpy array.
        obs = obs[:, :, 0:3]  # Remove alpha channel.
        obs = obs[:,:,::-1] # Convert from BGR to RGB
        obs = resize(obs, (144, 256, 3), order=0)  # Downsample image.
        obs = img_as_ubyte(obs)
        if self.render_mode == "rgb_array":
            self.frame = obs # Store array for rendering.
        return obs

    def reset(
        self, seed: int | None = None, options: dict | None = None
    ) -> tuple[NDArray, dict]:
        # Send array of size 1 if resetting and ignore response.
        msg = json.dumps([1])
        self.socket.send_string(msg)
        self.socket.recv()

        # No info to return.
        return self._get_obs(), {}

    def step(self, action: MultiBinary) -> tuple[NDArray, int, bool, bool, dict]:
        # Send action to C# side.
        action = action.astype(np.uint8).tolist()
        msg = json.dumps(action)
        self.socket.send_string(msg)

        # Retrieve results of action.
        msg = self.socket.recv()
        msg = json.loads(msg)
        reward = int(msg[0])
        terminated = msg[1] == "1"

        return self._get_obs(), reward, terminated, False, {}

    def render(self):
        if self.render_mode == "rgb_array":
            if self.frame == None:
                self._get_obs()
            return self.frame
