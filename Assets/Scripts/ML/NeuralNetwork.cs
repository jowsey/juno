using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ML
{
    public class NeuralNetwork
    {
        private readonly int[] _layers; // number of neurons in each layer
        private float[][] _neurons; // [layer][neuron]
        private float[][][] _weights; // [layer][toNeuron][fromNeuron]
        private float[][] _biases; // [layer][neuron]

        // new network with random weights
        public NeuralNetwork(int[] layers)
        {
            _layers = new int[layers.Length];
            Array.Copy(layers, _layers, layers.Length);

            InitialiseNeurons();
            InitialiseWeights();
        }

        // copy network from another
        public NeuralNetwork(NeuralNetwork other)
        {
            _layers = other._layers.Clone() as int[];

            InitialiseNeurons();
            InitialiseWeights(false);

            var otherGenes = other.GetFlatWeights();
            SetFlatWeights(otherGenes);
        }

        // crossover between two parents to create a child
        public NeuralNetwork(NeuralNetwork parent1, NeuralNetwork parent2)
        {
            _layers = parent1._layers.Clone() as int[];

            InitialiseNeurons();
            InitialiseWeights(false);

            var parent1Genes = parent1.GetFlatWeights();
            var parent2Genes = parent2.GetFlatWeights();

            var childGenes = new float[parent1Genes.Length];

            for (var i = 0; i < parent1Genes.Length; i++)
            {
                childGenes[i] = Random.value > 0.5f ? parent1Genes[i] : parent2Genes[i];
            }

            SetFlatWeights(childGenes);
        }

        // new empty neurons
        private void InitialiseNeurons()
        {
            _neurons = new float[_layers.Length][];
            for (var i = 0; i < _layers.Length; i++)
            {
                _neurons[i] = new float[_layers[i]];
            }
        }

        // random weights and biases
        private void InitialiseWeights(bool randomize = true)
        {
            _weights = new float[_layers.Length - 1][][];
            _biases = new float[_layers.Length - 1][];

            for (var i = 0; i < _layers.Length - 1; i++)
            {
                var neuronsInLayer = _layers[i + 1];
                var neuronsInPrevLayer = _layers[i];

                _weights[i] = new float[neuronsInLayer][];
                _biases[i] = new float[neuronsInLayer];

                var scale = randomize ? Mathf.Sqrt(2f / (neuronsInPrevLayer + neuronsInLayer)) : 0f;

                for (var j = 0; j < neuronsInLayer; j++)
                {
                    _weights[i][j] = new float[neuronsInPrevLayer];

                    if (randomize)
                    {
                        for (var k = 0; k < neuronsInPrevLayer; k++)
                        {
                            _weights[i][j][k] = Random.Range(-1f, 1f) * scale;
                        }
                    }

                    _biases[i][j] = randomize ? Random.Range(-0.5f, 0.5f) * scale : 0f;
                }
            }
        }

        // feed input through the network
        public float[] FeedForward(float[] inputs)
        {
            if (inputs.Length != _layers[0])
            {
                Debug.LogError($"Expected {_layers[0]} inputs, got {inputs.Length} :\\");
                return null;
            }

            // set input layer
            Array.Copy(inputs, _neurons[0], inputs.Length);

            // forward propagation
            for (var i = 1; i < _layers.Length; i++)
            {
                for (var j = 0; j < _neurons[i].Length; j++)
                {
                    var value = 0f;

                    // sum of weights * neurons from previous layer
                    for (var k = 0; k < _neurons[i - 1].Length; k++)
                    {
                        value += _weights[i - 1][j][k] * _neurons[i - 1][k];
                    }

                    // add bias and apply activation function
                    _neurons[i][j] = Activate(value + _biases[i - 1][j]);
                }
            }

            // output layer
            return _neurons[^1];
        }

        private float Activate(float value)
        {
            return (float)Math.Tanh(value);
        }

        // flatten weights and biases into an array
        public float[] GetFlatWeights()
        {
            var totalWeights = _weights.Sum(layer => layer.Sum(neurons => neurons.Length));
            var totalBiases = _biases.Sum(layer => layer.Length);

            var flatWeights = new float[totalWeights + totalBiases];
            var index = 0;

            for (var i = 0; i < _weights.Length; i++)
            {
                for (var j = 0; j < _weights[i].Length; j++)
                {
                    for (var k = 0; k < _weights[i][j].Length; k++)
                    {
                        flatWeights[index++] = _weights[i][j][k];
                    }
                }

                for (var j = 0; j < _biases[i].Length; j++)
                {
                    flatWeights[index++] = _biases[i][j];
                }
            }

            return flatWeights;
        }

        // load weights and biases from a flat array
        public void SetFlatWeights(float[] flatWeights)
        {
            var index = 0;

            for (var i = 0; i < _weights.Length; i++)
            {
                for (var j = 0; j < _weights[i].Length; j++)
                {
                    for (var k = 0; k < _weights[i][j].Length; k++)
                    {
                        _weights[i][j][k] = flatWeights[index++];
                    }
                }

                for (var j = 0; j < _biases[i].Length; j++)
                {
                    _biases[i][j] = flatWeights[index++];
                }
            }
        }
    }
}