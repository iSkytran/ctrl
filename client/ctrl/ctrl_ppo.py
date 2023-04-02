import gymnasium as gym
from ctrl import CtrlEnv
from stable_baselines3 import PPO
from stable_baselines3.common.env_checker import check_env
from stable_baselines3.common.env_util import make_vec_env
from stable_baselines3.common.vec_env import VecFrameStack

env = make_vec_env("ctrl", n_envs=1)
env = VecFrameStack(env, n_stack=4)
model = PPO("CnnPolicy", env)
model.learn(total_timesteps=1000000)
