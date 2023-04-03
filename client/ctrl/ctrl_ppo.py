import ctrl
import os
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.env_util import make_vec_env
from stable_baselines3.common.logger import configure
from stable_baselines3.common.vec_env import VecFrameStack

output_path = os.getenv("CTRL_OUTPUT_PATH", "./rl_out")
checkpoint_frequency = os.getenv("CTRL_CHECKPOINT_FREQUENCY", 100000)
timesteps = os.getenv("CTRL_TIMESTEPS", 20000000)

logger = configure(output_path, ["stdout", "log", "csv"])
checkpoint_callback = CheckpointCallback(
    save_freq=checkpoint_frequency,
    save_path=output_path,
    name_prefix="rl_model",
    save_replay_buffer=True,
    save_vecnormalize=True,
)

env = make_vec_env("ctrl", n_envs=1)
env = VecFrameStack(env, n_stack=4)
model = PPO("CnnPolicy", env)
model.learn(total_timesteps=timesteps, callback=checkpoint_callback)
model.save(output_path)
